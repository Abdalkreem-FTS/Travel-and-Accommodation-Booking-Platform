using HotelBooking.Application.Amenities.Dtos;

namespace HotelBooking.Application.Amenities;

public interface IAmenityQueries
{
    Task<IReadOnlyList<AmenityDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AmenityDto>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
