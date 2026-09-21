namespace HotelBooking.Domain.Idempotency;

public interface IIdempotencyRepository
{
    void Add(IdempotencyRecord record);

    Task<IdempotencyRecord?> FindAsync(
        Guid userId,
        string normalizedKey,
        string endpoint,
        CancellationToken cancellationToken = default);
}
