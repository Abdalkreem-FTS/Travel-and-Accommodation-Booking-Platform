using HotelBooking.Application.Amenities;
using HotelBooking.Application.Amenities.Dtos;
using HotelBooking.Domain.Amenities;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Queries;

internal sealed class AmenityQueries(HotelBookingDbContext context) : IAmenityQueries
{
    public async Task<IReadOnlyList<AmenityDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await ByName(context.Amenities.AsNoTracking()).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AmenityDto>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        Guid[] wanted = [.. ids];

        return await ByName(context.Amenities.AsNoTracking().Where(amenity => ((IEnumerable<Guid>)wanted).Contains(amenity.Id)))
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<AmenityDto> ByName(IQueryable<Amenity> amenities) =>
        amenities
            .OrderBy(amenity => amenity.Name)
            .ThenBy(amenity => amenity.Id)
            .Select(amenity => new AmenityDto(
                amenity.Id, amenity.Slug, amenity.Name, amenity.Description));
}
