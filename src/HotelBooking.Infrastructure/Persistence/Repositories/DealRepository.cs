using HotelBooking.Domain.Deals;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Repositories;

internal sealed class DealRepository(HotelBookingDbContext context) : IDealRepository
{
    public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Deals
            .Include(deal => deal.Nights)
            .FirstOrDefaultAsync(deal => deal.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Deal>> ListLiveForRoomsAsync(
        IReadOnlyCollection<Guid> roomIds,
        DateOnly from,
        DateOnly until,
        CancellationToken cancellationToken = default) =>
        await context.Deals
            .Where(deal => roomIds.Contains(deal.RoomId) && deal.StartsOn < until && from < deal.EndsOn)
            .ToListAsync(cancellationToken);

    public void Add(Deal deal) => context.Deals.Add(deal);
}
