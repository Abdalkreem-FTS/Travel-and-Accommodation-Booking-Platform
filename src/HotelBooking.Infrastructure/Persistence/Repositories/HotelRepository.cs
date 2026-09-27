using HotelBooking.Domain.Hotels;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Repositories;

internal sealed class HotelRepository(HotelBookingDbContext context) : IHotelRepository
{
    public Task<Hotel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Hotels.FirstOrDefaultAsync(hotel => hotel.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Hotels.AnyAsync(hotel => hotel.Id == id, cancellationToken);

    public Task<bool> ExistsInCityAsync(Guid cityId, CancellationToken cancellationToken = default) =>
        context.Hotels.AnyAsync(hotel => hotel.CityId == cityId, cancellationToken);

    public void Add(Hotel hotel) => context.Hotels.Add(hotel);
}
