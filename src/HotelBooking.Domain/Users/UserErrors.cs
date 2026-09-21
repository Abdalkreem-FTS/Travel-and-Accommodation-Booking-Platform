using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Users;

public static class UserErrors
{
    public static Error FirstNameRequired => Error.Validation(
        "User.FirstNameRequired", "firstName", "First name is required.");

    public static Error FirstNameTooLong => Error.Validation(
        "User.FirstNameTooLong", "firstName", $"First name must be {User.MaxNameLength} characters or fewer.");

    public static Error LastNameRequired => Error.Validation(
        "User.LastNameRequired", "lastName", "Last name is required.");

    public static Error LastNameTooLong => Error.Validation(
        "User.LastNameTooLong", "lastName", $"Last name must be {User.MaxNameLength} characters or fewer.");

    public static Error PasswordHashRequired => Error.Failure(
        "User.PasswordHashRequired", "A user cannot be registered without a password hash.");

    public static Error EmailAlreadyRegistered => Error.Conflict(
        "User.EmailAlreadyRegistered", "That email address is already registered.");

    public static Error NotFound => Error.NotFound(
        "User.NotFound", "No such user.");
}
