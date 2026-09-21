using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Bookings;

public sealed record ConfirmationNumber
{
    public const int Length = 17;

    private const string Prefix = "HB-";

    private const int GroupLength = 4;

    private ConfirmationNumber(string value) => Value = value;

    public string Value { get; }

    public static ConfirmationNumber From(Guid seed)
    {
        var hex = seed.ToString("N").ToUpperInvariant();

        return new ConfirmationNumber($"{Prefix}{hex[..4]}-{hex[4..8]}-{hex[8..12]}");
    }

    public static Result<ConfirmationNumber> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ConfirmationNumberErrors.Required;
        }

        var normalised = value.Trim().ToUpperInvariant();

        return IsWellFormed(normalised)
            ? new ConfirmationNumber(normalised)
            : ConfirmationNumberErrors.Invalid;
    }

    public override string ToString() => Value;

    private static bool IsWellFormed(string candidate)
    {
        if (candidate.Length != Length || !candidate.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var groups = candidate[Prefix.Length..].Split('-');

        return groups.Length == 3
               && Array.TrueForAll(
                   groups,
                   group => group.Length == GroupLength && group.All(char.IsAsciiHexDigitUpper));
    }
}
