using HotelBooking.Domain.Rooms;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Repositories;

internal sealed class RoomRepository(HotelBookingDbContext context) : IRoomRepository
{
    public Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Rooms.FirstOrDefaultAsync(room => room.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Room>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        ids.Count == 0
            ? []
            : await context.Rooms
                .Where(room => ids.Contains(room.Id))
                .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Rooms.AnyAsync(room => room.Id == id, cancellationToken);

    public Task<bool> ExistsInHotelAsync(Guid hotelId, CancellationToken cancellationToken = default) =>
        context.Rooms.AnyAsync(room => room.HotelId == hotelId, cancellationToken);

    public void Add(Room room) => context.Rooms.Add(room);
}
