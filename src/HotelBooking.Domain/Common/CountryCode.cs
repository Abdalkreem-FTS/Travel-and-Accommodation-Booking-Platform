using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

/// <summary>
/// An ISO 3166-1 alpha-2 country code
/// </summary>
public sealed record CountryCode
{
    public const int Length = 2;

    private CountryCode(string value) => Value = value;

    public string Value { get; }

    public static Result<CountryCode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CountryCodeErrors.Required;
        }

        var normalised = value.Trim().ToUpperInvariant();

        return normalised.Length == Length && normalised.All(char.IsAsciiLetterUpper)
            ? new CountryCode(normalised)
            : CountryCodeErrors.Invalid;
    }

    public override string ToString() => Value;
}
