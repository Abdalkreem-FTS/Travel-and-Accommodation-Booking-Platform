using HotelBooking.Domain.Bookings;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Repositories;

internal sealed class BookingRepository(HotelBookingDbContext context) : IBookingRepository
{
    public void Add(Booking booking) => context.Bookings.Add(booking);

    public Task<Booking?> GetWithNightsAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default) =>
        context.Bookings
            .Include(booking => booking.Nights)
            .FirstOrDefaultAsync(booking => booking.Id == bookingId, cancellationToken);

    public Task<Booking?> GetWithLinesAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default) =>
        context.Bookings
            .AsNoTracking()
            .Include(booking => booking.Lines)
            .FirstOrDefaultAsync(booking => booking.Id == bookingId, cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> FindSoldRoomsAsync(
        IReadOnlyCollection<RoomStay> stays,
        CancellationToken cancellationToken = default)
    {
        if (stays.Count == 0)
        {
            return [];
        }

        List<Guid> roomIds = [.. stays.Select(stay => stay.Room.Id).Distinct()];

        var from = stays.Min(stay => stay.Stay.CheckIn);
        var to = stays.Max(stay => stay.Stay.CheckOut);

        var sold = await context.RoomNightInventory
            .AsNoTracking()
            .Where(night => roomIds.Contains(night.RoomId) && night.StayDate >= from && night.StayDate < to)
            .Select(night => new { night.RoomId, night.StayDate })
            .ToListAsync(cancellationToken);

        return
        [
            .. stays
                .Where(stay => sold.Exists(night =>
                    night.RoomId == stay.Room.Id
                    && night.StayDate >= stay.Stay.CheckIn
                    && night.StayDate < stay.Stay.CheckOut))
                .Select(stay => stay.Room.Id)
                .Distinct()
        ];
    }

    public Task<bool> HasNightsFromAsync(
        Guid roomId,
        DateOnly fromDate,
        CancellationToken cancellationToken = default) =>
        context.RoomNightInventory
            .AsNoTracking()
            .AnyAsync(night => night.RoomId == roomId && night.StayDate >= fromDate, cancellationToken);
}
