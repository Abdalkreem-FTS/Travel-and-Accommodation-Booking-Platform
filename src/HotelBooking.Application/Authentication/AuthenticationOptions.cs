namespace HotelBooking.Application.Authentication;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(7);

    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan SessionRevocationWindow => AccessTokenLifetime + ClockSkew;
}
