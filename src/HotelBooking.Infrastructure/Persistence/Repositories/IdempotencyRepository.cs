using HotelBooking.Domain.Idempotency;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Repositories;

internal sealed class IdempotencyRepository(HotelBookingDbContext context) : IIdempotencyRepository
{
    public void Add(IdempotencyRecord record) => context.IdempotencyRecords.Add(record);


    public Task<IdempotencyRecord?> FindAsync(
        Guid userId,
        string normalizedKey,
        string endpoint,
        CancellationToken cancellationToken = default) =>
        context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                record => record.UserId == userId
                          && record.Key == normalizedKey
                          && record.Endpoint == endpoint,
                cancellationToken);
}
