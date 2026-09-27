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

    public static Error RoleNotGranted => Error.NotFound(
        "User.RoleNotGranted", "That user does not hold that role.");

    public static Error LastRoleCannotBeRevoked => Error.Conflict(
        "User.LastRoleCannotBeRevoked", "A user must keep at least one role.");

    public static Error RoleGrantRaced => Error.Conflict(
        "User.RoleGrantRaced", "That role was granted by another request. Try again.");

    public static Error UnknownRole => Error.Validation(
        "User.UnknownRole", "role", $"Role must be one of: {string.Join(", ", Enum.GetNames<UserRole>())}.");

    public static Error CannotChangeYourOwnRoles => Error.Forbidden(
        "User.CannotChangeYourOwnRoles",
        "You cannot revoke a role from yourself. Ask another administrator.");

    public static Error RoleRevocationIncomplete => Error.Unavailable(
        "User.RoleRevocationIncomplete",
        "The role was revoked and every session was ended, but access tokens already issued keep "
        + "the role until they expire, within one access-token lifetime. Nothing needs to be retried.");
}
