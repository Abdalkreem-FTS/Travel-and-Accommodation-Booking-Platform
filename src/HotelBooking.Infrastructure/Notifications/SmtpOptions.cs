namespace HotelBooking.Infrastructure.Notifications;

public sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 1025;

    public bool UseStartTls { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = "Hotel Booking";

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
