using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.RefreshTokens;

public static class RefreshTokenErrors
{
    public static Error Reused => Error.Unauthorized(
        "Auth.RefreshTokenReused", "This refresh token has already been used.");

    public static Error Expired => Error.Unauthorized(
        "Auth.RefreshTokenExpired", "This refresh token has expired.");

    public static Error LogoutUnavailable => Error.Unavailable(
        "Auth.LogoutUnavailable",
        "The session was revoked but the access token could not be revoked yet. Please retry.");
}
