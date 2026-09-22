namespace HotelBooking.Application.Authentication.Dtos;

public sealed record SessionDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc)
{
    public string TokenType { get; init; } = "Bearer";
}
