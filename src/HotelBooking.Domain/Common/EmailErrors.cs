using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

public static class EmailErrors
{
    private const string Field = "email";

    public static Error Required => Error.Validation(
        "Email.Required", Field, "Email is required.");

    public static Error Invalid => Error.Validation(
        "Email.Invalid", Field, "Email is not a valid address.");

    public static Error TooLong => Error.Validation(
        "Email.TooLong", Field, $"Email must be {Email.MaxLength} characters or fewer.");
}
