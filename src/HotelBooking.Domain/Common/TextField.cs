using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

/// <summary>
/// Trimming and length rules shared by the aggregates, trimming happens here, once, so a
/// padded name can never reach a unique index and create a second row that looks identical
/// </summary>
internal static class TextField
{
    public static string Require(string? value, int maxLength, Error required, Error tooLong, List<Error> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(required);

            return string.Empty;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            errors.Add(tooLong);
        }

        return trimmed;
    }

    public static string? Optional(string? value, int maxLength, Error tooLong, List<Error> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            errors.Add(tooLong);
        }

        return trimmed;
    }

    public static string RequireUrl(string? value, int maxLength, Error required, Error invalid, List<Error> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(required);

            return string.Empty;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength || !IsAbsoluteHttpUrl(trimmed))
        {
            errors.Add(invalid);
        }

        return trimmed;
    }

    public static string? OptionalUrl(string? value, int maxLength, Error invalid, List<Error> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length <= maxLength && IsAbsoluteHttpUrl(trimmed))
        {
            return trimmed;
        }

        errors.Add(invalid);

        return null;

    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal));
}
