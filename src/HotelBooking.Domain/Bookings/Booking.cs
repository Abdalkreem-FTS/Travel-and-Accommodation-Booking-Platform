using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings.Events;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

using Reason = HotelBooking.Domain.Bookings.CancellationReason;

namespace HotelBooking.Domain.Bookings;

public sealed class Booking : AggregateRoot<Guid>
{
    public const int MaxLines = 10;

    public static readonly TimeSpan CancellationWindow = TimeSpan.FromHours(48);

    private readonly List<BookingLine> _lines;
    private readonly List<RoomNight> _nights;

    private Booking(
        Guid id,
        Guid userId,
        Guid hotelId,
        List<BookingLine> lines,
        Money totalPrice,
        ConfirmationNumber confirmation,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        UserId = userId;
        HotelId = hotelId;
        TotalPrice = totalPrice;
        Confirmation = confirmation;
        Status = BookingStatus.Pending;
        CreatedAtUtc = createdAtUtc;

        _lines = lines;

        EarliestCheckIn = lines.Min(line => line.Stay.CheckIn);
        LatestCheckOut = lines.Max(line => line.Stay.CheckOut);

        _nights =
        [
            .. lines
                .SelectMany(line => line.SellNights())
                .OrderBy(night => night.RoomId)
                .ThenBy(night => night.StayDate)
        ];
    }

    private Booking()
    {
        _lines = [];
        _nights = [];
        TotalPrice = null!;
        Confirmation = null!;
    }

    public Guid UserId { get; private set; }

    public Guid HotelId { get; private set; }

    public DateOnly EarliestCheckIn { get; private set; }

    public DateOnly LatestCheckOut { get; private set; }

    public Money TotalPrice { get; private set; }

    public ConfirmationNumber Confirmation { get; private set; }

    public BookingStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public Reason? CancellationReason { get; private set; }

    public IReadOnlyList<BookingLine> Lines => _lines;

    public IReadOnlyList<RoomNight> Nights => _nights;

    private DateTimeOffset CancellationDeadline =>
        new DateTimeOffset(EarliestCheckIn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
        - CancellationWindow;

    public static Result<Booking> Reserve(
        Guid id,
        Guid userId,
        IReadOnlyList<RoomStay> stays,
        ConfirmationNumber confirmation,
        DateTimeOffset nowUtc)
    {
        List<Error> errors = [];

        if (userId == Guid.Empty)
        {
            errors.Add(BookingErrors.UserRequired);
        }

        switch (stays.Count)
        {
            case 0:
                errors.Add(BookingErrors.ItemsRequired);
                break;
            case > MaxLines:
                errors.Add(BookingErrors.TooManyItems);
                break;
            default:
                {
                    if (stays.Any(stay => stay.Room.HotelId != stays[0].Room.HotelId))
                    {
                        errors.Add(BookingErrors.LinesSpanHotels);
                    }

                    break;
                }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var lines = BuildLines(id, stays, errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        var totalPrice = BookingPricingService.Sum([.. lines.Select(line => line.LineTotal)]);

        if (totalPrice.IsError)
        {
            return totalPrice.Errors;
        }

        return new Booking(
            id, userId, stays[0].Room.HotelId, lines, totalPrice.Value, confirmation, nowUtc);
    }

    public Result<Updated> Confirm(Guid eventId, DateTimeOffset nowUtc)
    {
        if (Status is not BookingStatus.Pending)
        {
            return BookingErrors.InvalidTransition;
        }

        Status = BookingStatus.Confirmed;

        Raise(new BookingConfirmed(
            eventId,
            Id,
            UserId,
            HotelId,
            Confirmation.Value,
            EarliestCheckIn,
            LatestCheckOut,
            nowUtc));

        return Result.Updated;
    }

    public Result<Updated> CheckIn()
    {
        if (Status is not BookingStatus.Confirmed)
        {
            return BookingErrors.InvalidTransition;
        }

        Status = BookingStatus.CheckedIn;

        return Result.Updated;
    }

    public Result<Updated> Complete()
    {
        if (Status is not BookingStatus.CheckedIn)
        {
            return BookingErrors.InvalidTransition;
        }

        Status = BookingStatus.Completed;

        return Result.Updated;
    }

    public Result<Success> CanCancel(DateTimeOffset nowUtc)
    {
        if (Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
        {
            return BookingErrors.InvalidTransition;
        }

        return nowUtc > CancellationDeadline ? BookingErrors.CancellationWindowClosed : Result.Success;
    }

    public Result<Updated> Cancel(Guid eventId, DateTimeOffset nowUtc)
    {
        var allowed = CanCancel(nowUtc);

        if (allowed.IsError)
        {
            return allowed.Errors;
        }

        Release(eventId, Reason.RequestedByGuest, nowUtc);

        return Result.Updated;
    }

    public Result<Updated> Expire()
    {
        if (Status is not BookingStatus.Pending)
        {
            return BookingErrors.InvalidTransition;
        }

        Status = BookingStatus.Expired;

        _nights.Clear();

        return Result.Updated;
    }

    private void Release(Guid eventId, Reason reason, DateTimeOffset nowUtc)
    {
        Status = BookingStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        CancellationReason = reason;

        _nights.Clear();

        Raise(new BookingCancelled(
            eventId, Id, UserId, HotelId, Confirmation.Value, reason, nowUtc));
    }

    private static List<BookingLine> BuildLines(
        Guid id,
        IReadOnlyList<RoomStay> stays,
        List<Error> errors)
    {
        List<BookingLine> lines = [];

        for (var index = 0; index < stays.Count; index++)
        {
            var line = BookingLine.For(id, index + 1, stays[index]);

            if (line.IsError)
            {
                errors.AddRange(line.Errors);

                continue;
            }

            if (lines.Exists(held => held.ClaimsANightAlsoClaimedBy(line.Value)))
            {
                errors.Add(BookingErrors.OverlappingLines);

                continue;
            }

            lines.Add(line.Value);
        }

        return lines;
    }
}
