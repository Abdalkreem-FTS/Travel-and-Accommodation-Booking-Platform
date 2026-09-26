using System.Globalization;

using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Bookings;

public static class BookingErrors
{
    public static Error UserRequired => Error.Validation(
        "Booking.UserRequired", "userId", "A booking must belong to a user.");

    public static Error ItemsRequired => Error.Validation(
        "Booking.ItemsRequired", "items", "A booking needs at least one stay.");

    public static Error TooManyItems => Error.Validation(
        "Booking.TooManyItems",
        "items",
        string.Create(
            CultureInfo.InvariantCulture,
            $"A booking holds at most {Booking.MaxLines} stays. Check these out before booking more."));

    public static Error LinesSpanHotels => Error.Validation(
        "Booking.LinesSpanHotels",
        "items",
        "Every stay in one booking must be at the same hotel. Book each hotel separately.");

    public static Error OverlappingLines => Error.Validation(
        "Booking.OverlappingLines",
        "items",
        "Two stays in this booking claim the same room on the same night.");

    public static Error RoomCannotHostParty => Error.Validation(
        "Booking.RoomCannotHostParty", "adults", "That room cannot host a party of that size.");

    public static Error CurrencyMismatch => Error.Conflict(
        "Booking.CurrencyMismatch",
        "Every stay in one booking must be priced in the same currency.");

    public static Error RoomUnavailable => Error.Conflict(
        "Booking.RoomUnavailable", "That room is already booked for one or more of those nights.");

    public static Error PaymentPending => Error.Conflict(
        "Booking.PaymentPending",
        "You already have a booking waiting for payment. Pay for it or cancel it before booking again.");

    public static Error PaymentJustCompleted => Error.Conflict(
        "Booking.PaymentJustCompleted",
        "The payment for this booking has just gone through, so it is being confirmed. Cancel it again once it shows as confirmed.");

    public static Error ConfirmationNumberCollision => Error.Conflict(
        "Booking.ConfirmationNumberCollision",
        "Could not allocate a confirmation number for this booking. Please submit it again.");

    public static Error InvalidTransition => Error.Conflict(
        "Booking.InvalidTransition", "That booking cannot move to that state from where it is.");

    public static Error CancellationWindowClosed => Error.Conflict(
        "Booking.CancellationWindowClosed",
        string.Create(
            CultureInfo.InvariantCulture,
            $"A booking can only be cancelled up to {Booking.CancellationWindow.TotalHours:0} hours before check-in."));

    public static Error NotFound => Error.NotFound(
        "Booking.NotFound", "No such booking.");
}
