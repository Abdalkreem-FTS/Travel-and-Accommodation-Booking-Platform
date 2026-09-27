namespace HotelBooking.Application.Common;

public sealed record AccessToken(string Value, string Jti, DateTimeOffset ExpiresAtUtc);
