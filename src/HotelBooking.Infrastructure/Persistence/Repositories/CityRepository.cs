using HotelBooking.Domain.Cities;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Repositories;

internal sealed class CityRepository(HotelBookingDbContext context) : ICityRepository
{
    public Task<City?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Cities.FirstOrDefaultAsync(city => city.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Cities.AnyAsync(city => city.Id == id, cancellationToken);

    public void Add(City city) => context.Cities.Add(city);
}
