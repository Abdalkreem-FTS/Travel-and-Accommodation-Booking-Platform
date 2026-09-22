namespace HotelBooking.Api.RateLimiting;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    public bool Enabled { get; init; } = true;

    public WindowLimit Auth { get; init; } = new() { Permit = 10, WindowMinutes = 15 };

    public WindowLimit Global { get; init; } = new() { Permit = 300, WindowMinutes = 1 };
}

public sealed class WindowLimit
{
    public int Permit { get; init; }

    public int WindowMinutes { get; init; }

    public bool IsPositive => Permit > 0 && WindowMinutes > 0;

    public TimeSpan Window => TimeSpan.FromMinutes(WindowMinutes);
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
}
