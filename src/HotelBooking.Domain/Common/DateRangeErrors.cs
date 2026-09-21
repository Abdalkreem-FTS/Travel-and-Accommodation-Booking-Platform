using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

public static class DateRangeErrors
{
    public static Error CheckOutNotAfterCheckIn => Error.Validation(
        "DateRange.CheckOutNotAfterCheckIn", "checkOut", "Check-out must be after check-in.");

    public static Error CheckInInThePast => Error.Validation(
        "DateRange.CheckInInThePast", "checkIn", "Check-in cannot be in the past.");

    public static Error TooManyNights => Error.Validation(
        "DateRange.TooManyNights", "checkOut", $"A stay may run for at most {DateRange.MaxNights} nights.");
}
