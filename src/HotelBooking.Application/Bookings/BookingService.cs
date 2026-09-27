using System.Diagnostics;

using FluentValidation;

using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Bookings.Dtos;
using HotelBooking.Application.Common;
using HotelBooking.Application.Payments;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Carts;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Idempotency;
using HotelBooking.Domain.Payments;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Bookings;

public sealed class BookingService(
    IBookingRepository bookingRepository,
    IPaymentRepository paymentRepository,
    IRoomRepository roomRepository,
    IDealRepository dealRepository,
    ICartRepository cartRepository,
    IIdempotencyRepository idempotencyRepository,
    IPaymentProvider paymentProvider,
    IUnitOfWork unitOfWork,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    IValidator<CreateBookingRequest> validator,
    ILogger<BookingService> logger) : IBookingService
{
    private const string Endpoint = "POST /bookings";

    private static readonly TimeSpan CheckoutTimeout = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan AbandonTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan CartCleanupTimeout = TimeSpan.FromSeconds(5);

    public async Task<Result<BookingDto>> CreateAsync(
        CreateBookingRequest request,
        Guid userId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();

        var result = await AttemptAsync(request, userId, idempotencyKey, cancellationToken);

        Telemetry.BookingDuration.Record(
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            new KeyValuePair<string, object?>("outcome", Outcome(result)));

        return result;
    }

    public async Task<Result<BookingDto>> GetAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var booking = await bookingRepository.GetWithLinesAsync(bookingId, cancellationToken);

        if (booking is null || booking.UserId != userId)
        {
            return BookingErrors.NotFound;
        }

        var payment = await paymentRepository.GetForBookingAsync(bookingId, cancellationToken);

        return BookingDto.From(booking, payment);
    }

    private static string Outcome(Result<BookingDto> result) =>
        result.IsError ? result.Errors[0].Type.ToString().ToLowerInvariant() : "created";

    private async Task<Result<BookingDto>> AttemptAsync(
        CreateBookingRequest request,
        Guid userId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var validationErrors = await validator.ValidateToErrorsAsync(request, cancellationToken);

        if (validationErrors.Count > 0)
        {
            return validationErrors;
        }

        var key = IdempotencyRecord.NormalizeKey(idempotencyKey);

        if (key.IsError)
        {
            return key.Errors;
        }

        var replay = await ReplayAsync(userId, key.Value, cancellationToken);

        if (replay is not null)
        {
            return replay;
        }

        var items = await ResolveItemsAsync(request, userId, cancellationToken);

        if (items.IsError)
        {
            return items.Errors;
        }

        var stays = await ToStaysAsync(items.Value, cancellationToken);

        if (stays.IsError)
        {
            return stays.Errors;
        }

        var booking = Booking.Reserve(
            guidProvider.NewSortable(),
            userId,
            stays.Value,
            ConfirmationNumber.From(guidProvider.NewOpaque()),
            dateTimeProvider.UtcNow);

        if (booking.IsError)
        {
            return booking.Errors;
        }

        var payment = Payment.Start(guidProvider.NewSortable(), booking.Value, dateTimeProvider.UtcNow);

        if (payment.IsError)
        {
            return payment.Errors;
        }

        var reserved = await unitOfWork.ExecuteInTransactionAsync(
            async token => await ReserveAsync(
                userId, key.Value, booking.Value, payment.Value, stays.Value, token),
            cancellationToken);

        if (!reserved.IsError)
        {
            return await OpenCheckoutAsync(userId, booking.Value, payment.Value, stays.Value, cancellationToken);
        }

        CountConflict(reserved.TopError);

        return reserved.Errors;

    }

    private async Task<Result<BookingDto>?> ReplayAsync(
        Guid userId,
        string key,
        CancellationToken cancellationToken)
    {
        var record = await idempotencyRepository.FindAsync(userId, key, Endpoint, cancellationToken);

        if (record?.BookingId is not { } bookingId)
        {
            return null;
        }

        var booking = await bookingRepository.GetWithLinesAsync(bookingId, cancellationToken);

        if (booking is null)
        {
            logger.LogError(
                "The idempotency key replayed by user {UserId} names booking {BookingId}, which is "
                + "not there; the guest cannot be told what their checkout did",
                userId, bookingId);

            return IdempotencyErrors.ResultMissing;
        }

        var payment = await paymentRepository.GetForBookingAsync(bookingId, cancellationToken);

        Telemetry.IdempotentReplays.Add(1);

        logger.LogInformation(
            "Answered user {UserId} with booking {BookingId} again: the same idempotency key came "
            + "back, so nothing was reserved or charged a second time",
            userId, bookingId);

        return BookingDto.From(booking, payment);
    }

    private async Task<Result<Success>> ReserveAsync(
        Guid userId,
        string idempotencyKey,
        Booking booking,
        Payment payment,
        IReadOnlyList<RoomStay> stays,
        CancellationToken cancellationToken)
    {
        idempotencyRepository.Add(
            IdempotencyRecord.Claim(
                userId, idempotencyKey, Endpoint, booking.Id, dateTimeProvider.UtcNow));

        var claimed = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (claimed.IsError)
        {
            return claimed.Errors;
        }

        var sold = await bookingRepository.FindSoldRoomsAsync(stays, cancellationToken);

        if (sold.Count > 0)
        {
            logger.LogInformation(
                "Rooms {RoomIds} are already sold for the nights this checkout asked for",
                sold.ToArray());

            return BookingErrors.RoomUnavailable;
        }

        bookingRepository.Add(booking);
        paymentRepository.Add(payment);

        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Result<BookingDto>> OpenCheckoutAsync(
        Guid userId,
        Booking booking,
        Payment payment,
        IReadOnlyList<RoomStay> stays,
        CancellationToken cancellationToken)
    {
        var checkout = await CreateCheckoutAsync(payment);

        if (checkout.IsError)
        {
            await AbandonAsync(booking.Id);

            return PaymentErrors.ProviderUnavailable;
        }

        var attached = payment.AttachCheckout(checkout.Value.Id, checkout.Value.Url);

        if (attached.IsError)
        {
            return attached.Errors;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            logger.LogError(
                "Checkout {CheckoutId} was opened for payment {PaymentId} but could not be saved "
                + "({ErrorCode}); booking {BookingId} stays held until the payment expires at {ExpiresAtUtc}",
                checkout.Value.Id, payment.Id, saved.TopError.Code, booking.Id, payment.ExpiresAtUtc);

            return saved.Errors;
        }

        await DropBookedStaysFromCartAsync(userId, stays);

        Telemetry.BookingsCreated.Add(1);
        Telemetry.PaymentsStarted.Add(1);

        logger.LogInformation(
            "Reserved booking {BookingId} for user {UserId}: {LineCount} stay(s) at hotel {HotelId}, "
            + "{Amount} {Currency} due by {ExpiresAtUtc} through payment {PaymentId}, checkout {CheckoutId}",
            booking.Id, userId, booking.Lines.Count, booking.HotelId, payment.Amount.Amount,
            payment.Amount.Currency, payment.ExpiresAtUtc, payment.Id, checkout.Value.Id);

        return BookingDto.From(booking, payment);
    }

    private async Task<Result<ProviderCheckout>> CreateCheckoutAsync(Payment payment)
    {
        using var budget = Budget(CheckoutTimeout);

        try
        {
            var checkout = await paymentProvider.CreateCheckoutAsync(payment, budget.Token);

            if (checkout.IsError)
            {
                logger.LogWarning(
                    "The payment provider refused a checkout for payment {PaymentId} ({ErrorCode})",
                    payment.Id, checkout.TopError.Code);
            }

            return checkout;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception, "The payment provider could not open a checkout for payment {PaymentId}", payment.Id);

            return PaymentErrors.ProviderUnavailable;
        }
    }

    private async Task AbandonAsync(Guid bookingId)
    {
        using var budget = Budget(AbandonTimeout);

        try
        {
            var abandoned = await unitOfWork.ExecuteInTransactionAsync<Updated>(
                async token =>
                {
                    var booking = await bookingRepository.GetWithNightsAsync(bookingId, token);
                    var payment = await paymentRepository.GetForBookingAsync(bookingId, token);

                    if (booking is null || payment is null)
                    {
                        return BookingErrors.NotFound;
                    }

                    var expired = booking.Expire();

                    if (expired.IsError)
                    {
                        return expired.Errors;
                    }

                    var ended = payment.Expire(dateTimeProvider.UtcNow);

                    if (ended.IsError)
                    {
                        return ended.Errors;
                    }

                    var saved = await unitOfWork.SaveChangesAsync(token);

                    return saved.IsError ? saved.Errors : Result.Updated;
                },
                budget.Token);

            if (abandoned.IsSuccess)
            {
                Telemetry.PaymentsExpired.Add(
                    1, new KeyValuePair<string, object?>("reason", "checkout_unavailable"));

                logger.LogWarning(
                    "Expired booking {BookingId} and released its nights: no checkout could be opened",
                    bookingId);

                return;
            }

            logger.LogError(
                "Booking {BookingId} could not be expired after its checkout failed ({ErrorCode}); its "
                + "nights stay held until its payment expires",
                bookingId, abandoned.TopError.Code);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Booking {BookingId} could not be expired after its checkout failed; its nights stay "
                + "held until its payment expires",
                bookingId);
        }
    }

    private async Task<Result<List<BookingItemRequest>>> ResolveItemsAsync(
        CreateBookingRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (request.Items is { } items)
        {
            return items.ToList();
        }

        var cart = await cartRepository.GetAsync(userId, cancellationToken);

        if (cart.IsError)
        {
            return cart.Errors;
        }

        var bookable = cart.Value.Items
            .Where(item => item.IsStillBookable(Today))
            .ToList();

        if (bookable.Count == 0)
        {
            return CartErrors.Empty;
        }

        return bookable
            .Select(item => new BookingItemRequest(
                item.RoomId,
                item.Stay.CheckIn,
                item.Stay.CheckOut,
                item.Guests.Adults,
                item.Guests.Children))
            .ToList();
    }

    private async Task<Result<List<RoomStay>>> ToStaysAsync(
        IReadOnlyList<BookingItemRequest> items,
        CancellationToken cancellationToken)
    {
        var rooms = await roomRepository.GetByIdsAsync(
            [.. items.Select(item => item.RoomId).Distinct()], cancellationToken);

        List<Error> errors = [];
        List<RoomStay> stays = [];

        foreach (var item in items)
        {
            var room = rooms.FirstOrDefault(candidate => candidate.Id == item.RoomId);

            if (room is null)
            {
                return RoomErrors.NotFound;
            }

            var stay = DateRange.Create(item.CheckIn, item.CheckOut, Today);
            var guests = Occupancy.Create(item.Adults, item.Children);

            errors.AddRange([.. stay.Errors, .. guests.Errors]);

            if (errors.Count == 0)
            {
                stays.Add(new RoomStay(room, stay.Value, guests.Value));
            }
        }

        return errors.Count > 0 ? errors : await WithLiveDealsAsync(stays, cancellationToken);
    }

    private async Task<List<RoomStay>> WithLiveDealsAsync(
        List<RoomStay> stays,
        CancellationToken cancellationToken)
    {
        if (stays.Count == 0)
        {
            return stays;
        }

        var deals = await dealRepository.ListLiveForRoomsAsync(
            [.. stays.Select(stay => stay.Room.Id).Distinct()],
            stays.Min(stay => stay.Stay.CheckIn),
            stays.Max(stay => stay.Stay.CheckOut),
            cancellationToken);

        if (deals.Count == 0)
        {
            return stays;
        }

        return
        [
            .. stays.Select(stay => stay with
            {
                Deals = [.. deals.Where(deal => deal.RoomId == stay.Room.Id)]
            })
        ];
    }

    private async Task DropBookedStaysFromCartAsync(Guid userId, IReadOnlyList<RoomStay> stays)
    {
        List<string> booked = [.. stays.Select(stay => CartItem.IdFor(stay.Room.Id, stay.Stay))];

        using var cleanup = Budget(CartCleanupTimeout);

        var removed = await cartRepository.RemoveItemsAsync(userId, booked, cleanup.Token);

        if (removed.IsError)
        {
            logger.LogWarning(
                "Booked {StayCount} stay(s) for user {UserId} but could not drop them from the cart; "
                + "the guest will keep seeing stays they have already booked",
                booked.Count, userId);
        }
    }

    private static CancellationTokenSource Budget(TimeSpan limit) => new(limit);

    private DateOnly Today => DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

    private static void CountConflict(Error error)
    {
        if (error == BookingErrors.RoomUnavailable)
        {
            Telemetry.BookingConflicts.Add(1);
        }
        else if (error == IdempotencyErrors.RequestInProgress)
        {
            Telemetry.IdempotentRequestsInProgress.Add(1);
        }
    }
}
