using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Users;

public static class AuthErrors
{
    public static Error InvalidCredentials => Error.Unauthorized(
        "Auth.InvalidCredentials", "Email or password is incorrect.");

    public static Error InvalidRefreshToken => Error.Unauthorized(
        "Auth.InvalidRefreshToken", "The refresh token is not valid.");
}
