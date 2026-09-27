using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

public sealed record Email
{
    public const int MaxLength = 254;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EmailErrors.Required;
        }

        var normalised = value.Trim().ToLowerInvariant();

        if (normalised.Length > MaxLength)
        {
            return EmailErrors.TooLong;
        }

        return IsWellFormed(normalised) ? new Email(normalised) : EmailErrors.Invalid;
    }

    public override string ToString() => Value;

    private static bool IsWellFormed(string candidate)
    {
        var separator = candidate.IndexOf('@', StringComparison.Ordinal);

        if (separator <= 0 || separator != candidate.LastIndexOf('@'))
        {
            return false;
        }

        var local = candidate[..separator];
        var domain = candidate[(separator + 1)..];

        if (local.Length == 0 || domain.Length == 0 || candidate.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var dot = domain.LastIndexOf('.');

        return dot > 0 && dot < domain.Length - 1;
    }
}
