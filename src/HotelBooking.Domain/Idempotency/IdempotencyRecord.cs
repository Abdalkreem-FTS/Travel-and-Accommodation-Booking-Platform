using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Idempotency;

public sealed class IdempotencyRecord
{
    public const int MaxKeyLength = 128;

    public const int MaxEndpointLength = 100;

    private IdempotencyRecord(
        Guid userId,
        string key,
        string endpoint,
        Guid bookingId,
        DateTimeOffset createdAtUtc)
    {
        UserId = userId;
        Key = key;
        Endpoint = endpoint;
        BookingId = bookingId;
        CreatedAtUtc = createdAtUtc;
    }

    private IdempotencyRecord()
    {
        Key = null!;
        Endpoint = null!;
    }

    public Guid UserId { get; private set; }

    public string Key { get; private set; }

    public string Endpoint { get; private set; }

    public Guid? BookingId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<string> NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return IdempotencyErrors.KeyRequired;
        }

        var trimmed = key.Trim();

        return trimmed.Length > MaxKeyLength ? IdempotencyErrors.KeyTooLong : trimmed;
    }

    public static IdempotencyRecord Claim(
        Guid userId,
        string normalizedKey,
        string endpoint,
        Guid bookingId,
        DateTimeOffset nowUtc) =>
        new(userId, normalizedKey, endpoint, bookingId, nowUtc);
}
