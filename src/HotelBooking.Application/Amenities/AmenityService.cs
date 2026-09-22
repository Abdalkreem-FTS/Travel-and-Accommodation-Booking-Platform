using HotelBooking.Application.Amenities.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Amenities;

public sealed class AmenityService(IAmenityQueries amenityQueries) : IAmenityService
{
    public async Task<Result<IReadOnlyList<AmenityDto>>> ListAsync(
        CancellationToken cancellationToken = default) =>
        Result<IReadOnlyList<AmenityDto>>.From(await amenityQueries.ListAsync(cancellationToken));
}
