using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Common;

public sealed class ConcurrencyToken
{
    private const int VersionLength = 8;

    private ConcurrencyToken(byte[] value, string version)
    {
        Value = value;
        Version = version;
    }

    public byte[] Value { get; }

    public string Version { get; }

    public string ETag => $"\"{Version}\"";

    public static ConcurrencyToken From(byte[] rowVersion) =>
        new(rowVersion, Convert.ToBase64String(rowVersion));

    public static Result<ConcurrencyToken> Create(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return ConcurrencyErrors.VersionRequired;
        }

        var unquoted = Unquote(headerValue.Trim());
        var buffer = new byte[unquoted.Length];

        var readable = Convert.TryFromBase64String(unquoted, buffer, out var written);

        return readable && written == VersionLength
            ? From(buffer[..written])
            : ConcurrencyErrors.VersionMalformed;
    }

    private static string Unquote(string value) =>
        value is ['"', _, ..] && value[^1] == '"'
            ? value[1..^1]
            : value;
}
