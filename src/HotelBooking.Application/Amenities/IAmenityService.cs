using HotelBooking.Application.Amenities.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Amenities;

public interface IAmenityService
{
    Task<Result<IReadOnlyList<AmenityDto>>> ListAsync(CancellationToken cancellationToken = default);
}
