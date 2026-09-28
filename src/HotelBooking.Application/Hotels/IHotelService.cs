using HotelBooking.Application.Common;
using HotelBooking.Application.Hotels.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Hotels;

public interface IHotelService
{
    Task<Result<HotelDto>> CreateAsync(
        CreateHotelRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<HotelDto>> GetAsync(
        Guid id,
        Guid? viewerId = null,
        string? clientAddress = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HotelImageDto>>> GetGalleryAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<PagedList<CityHotelDto>>> ListForCityAsync(
        Guid cityId,
        CityHotelsRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<HotelDto>> UpdateAsync(
        Guid id,
        UpdateHotelRequest request,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<Result<Deleted>> DeleteAsync(
        Guid id,
        string? ifMatch,
        CancellationToken cancellationToken = default);
}
