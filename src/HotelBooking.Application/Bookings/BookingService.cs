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
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Bookings;

public sealed class BookingService(
    IBookingRepository bookingRepository,
    IRoomRepository roomRepository,
    IDealRepository dealRepository,
    ICartRepository cartRepository,
    IIdempotencyRepository idempotencyRepository,
    IBookingCancellationService bookingCancellationService,
    IPaymentGateway paymentGateway,
    IUnitOfWork unitOfWork,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    IValidator<CreateBookingRequest> validator,
    ILogger<BookingService> logger) : IBookingService
{
    private const string Endpoint = "POST /bookings";

    private static readonly TimeSpan SettlementTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan CompensationTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan HoldReleaseTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan CartCleanupTimeout = TimeSpan.FromSeconds(5);

    private enum Hold
    {
        Release,
        Captured,
        Elsewhere
    }

    private readonly record struct Settlement(Result<BookingDto> Answer, Hold Hold);

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

        var booking = Booking.Create(
            guidProvider.NewSortable(),
            userId,
            stays.Value,
            ConfirmationNumber.From(guidProvider.NewOpaque()),
            guidProvider.NewSortable(),
            dateTimeProvider.UtcNow);

        if (booking.IsError)
        {
            return booking.Errors;
        }

        return await CheckOutAsync(userId, key.Value, booking.Value, stays.Value, cancellationToken);
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

        Telemetry.IdempotentReplays.Add(1);

        logger.LogInformation(
            "Answered user {UserId} with booking {BookingId} again: the same idempotency key came "
            + "back, so nothing was booked or charged a second time",
            userId, bookingId);

        return BookingDto.From(booking);
    }

    private async Task<Result<BookingDto>> CheckOutAsync(
        Guid userId,
        string idempotencyKey,
        Booking booking,
        IReadOnlyList<RoomStay> stays,
        CancellationToken cancellationToken)
    {
        var authorization = await paymentGateway.AuthorizeAsync(
            userId, booking.TotalPrice, AuthorizationReference(userId, idempotencyKey), cancellationToken);

        if (authorization.IsError)
        {
            CountPaymentFailure(authorization.TopError);

            logger.LogInformation(
                "Refused checkout for user {UserId}: the payment was not authorized ({ErrorCode})",
                userId, authorization.TopError.Code);

            return authorization.Errors;
        }

        var hold = Hold.Release;

        try
        {
            var written = await unitOfWork.ExecuteInTransactionAsync(
                async token => await WriteBookingAsync(userId, idempotencyKey, booking, stays, token),
                cancellationToken);

            if (written.IsError)
            {
                hold = Abandon(written.TopError);

                return written.Errors;
            }

            var settled = await SettleAsync(userId, written.Value, authorization.Value, stays);

            hold = settled.Hold;

            return settled.Answer;
        }
        finally
        {
            if (hold is Hold.Release)
            {
                await ReleaseHoldAsync(authorization.Value, booking.Id);
            }
        }
    }

    private async Task<Settlement> SettleAsync(
        Guid userId,
        BookingDto booking,
        PaymentAuthorization authorization,
        IReadOnlyList<RoomStay> stays)
    {
        Result<Success> captured;

        using (var settlement = Budget(SettlementTimeout))
        {
            try
            {
                captured = await paymentGateway.CaptureAsync(authorization, settlement.Token);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception, "Capture threw for booking {BookingId}; treating it as a failure", booking.Id);

                captured = PaymentErrors.CaptureFailed;
            }
        }

        if (captured.IsError)
        {
            return await CompensateAsync(booking);
        }

        await DropBookedStaysFromCartAsync(userId, stays);

        Telemetry.BookingsCreated.Add(1);

        logger.LogInformation(
            "Confirmed booking {BookingId} for user {UserId}: {LineCount} stay(s) at hotel {HotelId}, "
            + "{CheckIn} to {CheckOut}, {Amount} {Currency} captured",
            booking.Id, userId, booking.Lines.Count, booking.HotelId,
            booking.CheckIn, booking.CheckOut, booking.TotalAmount, booking.Currency);

        return new Settlement(booking, Hold.Captured);
    }

    private async Task<Result<BookingDto>> WriteBookingAsync(
        Guid userId,
        string idempotencyKey,
        Booking booking,
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

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        return saved.IsError ? saved.Errors : BookingDto.From(booking);
    }

    private static Hold Abandon(Error failure)
    {
        CountConflict(failure);

        return failure == IdempotencyErrors.RequestInProgress ? Hold.Elsewhere : Hold.Release;
    }

    private async Task<Settlement> CompensateAsync(BookingDto booking)
    {
        Telemetry.PaymentsFailed.Add(1, new KeyValuePair<string, object?>("reason", "capture"));

        logger.LogError(
            "Capture failed for booking {BookingId}; voiding it and releasing its nights", booking.Id);

        if (await TryVoidAsync(booking.Id))
        {
            return new Settlement(PaymentErrors.CaptureFailed, Hold.Release);
        }

        Telemetry.BookingVoidsFailed.Add(1);

        return new Settlement(PaymentErrors.CaptureFailedAndNotReleased, Hold.Elsewhere);
    }

    private async Task<bool> TryVoidAsync(Guid bookingId)
    {
        using var compensation = Budget(CompensationTimeout);

        try
        {
            var voided = await bookingCancellationService.VoidForFailedPaymentAsync(
                bookingId, compensation.Token);

            if (voided.IsSuccess)
            {
                return true;
            }

            logger.LogError(
                "Booking {BookingId} could not be voided after its capture failed ({ErrorCode}); its "
                + "nights are still sold and its authorization is still held",
                bookingId, voided.TopError.Code);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Booking {BookingId} could not be voided after its capture failed; its nights are "
                + "still sold and its authorization is still held",
                bookingId);
        }

        return false;
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

    private async Task ReleaseHoldAsync(PaymentAuthorization authorization, Guid bookingId)
    {
        using var release = Budget(HoldReleaseTimeout);

        try
        {
            var released = await paymentGateway.VoidAsync(authorization, release.Token);

            if (released.IsSuccess)
            {
                return;
            }

            logger.LogWarning(
                "Authorization {AuthorizationId} for booking {BookingId} could not be voided; the "
                + "guest's money stays held until it expires at the provider",
                authorization.Id, bookingId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Authorization {AuthorizationId} for booking {BookingId} could not be voided; the "
                + "guest's money stays held until it expires at the provider",
                authorization.Id, bookingId);
        }

        Telemetry.PaymentHoldsNotReleased.Add(1);
    }

    private static CancellationTokenSource Budget(TimeSpan limit) => new(limit);

    private static string AuthorizationReference(Guid userId, string idempotencyKey) =>
        $"{userId:N}:{idempotencyKey}";

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

    private static void CountPaymentFailure(Error error) =>
        Telemetry.PaymentsFailed.Add(
            1,
            new KeyValuePair<string, object?>(
                "reason", error == PaymentErrors.Declined ? "declined" : "authorize"));
}
