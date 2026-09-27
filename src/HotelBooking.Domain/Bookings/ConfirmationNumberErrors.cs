using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Bookings;

public static class ConfirmationNumberErrors
{
    public static Error Required => Error.Validation(
        "ConfirmationNumber.Required", "confirmationNumber", "A confirmation number is required.");

    public static Error Invalid => Error.Validation(
        "ConfirmationNumber.Invalid", "confirmationNumber", "That is not a well-formed confirmation number.");
}
