using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Amenities;
using HotelBooking.Application.Amenities.Dtos;
using HotelBooking.Application.Common;
using HotelBooking.Application.Hotels.Dtos;
using HotelBooking.Application.Visits;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Amenities;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Hotels;

public sealed class HotelService(
    IHotelRepository hotelRepository,
    IHotelQueries hotelQueries,
    ICityRepository cityRepository,
    IRoomRepository roomRepository,
    IAmenityQueries amenityQueries,
    ICacheService cacheService,
    IVisitTracker visitTracker,
    IUnitOfWork unitOfWork,
    IConcurrencyGuard concurrencyGuard,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<HotelService> logger) : IHotelService
{
    public async Task<Result<HotelDto>> CreateAsync(
        CreateHotelRequest request,
        CancellationToken cancellationToken = default)
    {
        var placement = await PlaceAsync(
            request.CityId, request.StarRating, request.Latitude, request.Longitude, cancellationToken);

        if (placement.IsError)
        {
            return placement.Errors;
        }

        var gallery = ReadGallery(request.Images);

        if (gallery.IsError)
        {
            return gallery.Errors;
        }

        var linked = await ReadAmenitiesAsync(request.AmenityIds, cancellationToken);

        if (linked.IsError)
        {
            return linked.Errors;
        }

        var hotel = Hotel.Create(
            guidProvider.NewSortable(),
            request.CityId,
            request.Name,
            request.Description,
            request.Owner,
            placement.Value.StarRating,
            placement.Value.Location,
            request.ThumbnailUrl,
            dateTimeProvider.UtcNow,
            gallery.Value,
            request.AmenityIds);

        if (hotel.IsError)
        {
            return hotel.Errors;
        }

        hotelRepository.Add(hotel.Value);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        logger.LogInformation(
            "Created hotel {HotelId} in city {CityId} with {ImageCount} images and {AmenityCount} amenities",
            hotel.Value.Id, request.CityId, hotel.Value.Images.Count, hotel.Value.AmenityIds.Count);

        return HotelDto.From(hotel.Value, linked.Value, concurrencyGuard.TokenFor(hotel.Value));
    }

    public async Task<Result<HotelDto>> GetAsync(
        Guid id,
        Guid? viewerId = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await cacheService.GetOrSetAsync(
            HotelCacheKeys.Details(id), ReadAsync, HotelCacheKeys.DetailsTimeToLive, cancellationToken);

        if (cached is null)
        {
            return HotelErrors.NotFound;
        }

        try
        {
            await visitTracker.RecordHotelViewAsync(id, cached.CityId, viewerId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The view of hotel {HotelId} was not counted", id);
        }

        return cached;

        async Task<HotelDto?> ReadAsync(CancellationToken token)
        {
            var hotel = await hotelRepository.GetByIdAsync(id, token);

            if (hotel is null)
            {
                return null;
            }

            var linked = await amenityQueries.ListByIdsAsync(hotel.AmenityIds, token);

            return HotelDto.From(hotel, linked, concurrencyGuard.TokenFor(hotel));
        }
    }

    public async Task<Result<IReadOnlyList<HotelImageDto>>> GetGalleryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var hotel = await hotelRepository.GetByIdAsync(id, cancellationToken);

        return hotel is null
            ? HotelErrors.NotFound
            : Result<IReadOnlyList<HotelImageDto>>.From([.. hotel.Images.Select(HotelImageDto.From)]);
    }

    public async Task<Result<PagedList<CityHotelDto>>> ListForCityAsync(
        Guid cityId,
        CityHotelsRequest request,
        CancellationToken cancellationToken = default)
    {
        List<Error> errors = [];

        var paging = PageRequest.Read(request.Page, request.PageSize, errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        if (!await cityRepository.ExistsAsync(cityId, cancellationToken))
        {
            return CityErrors.NotFound;
        }

        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        return await hotelQueries.ListForCityAsync(cityId, search, paging, cancellationToken);
    }

    public async Task<Result<HotelDto>> UpdateAsync(
        Guid id,
        UpdateHotelRequest request,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var version = ConcurrencyToken.Create(ifMatch);

        if (version.IsError)
        {
            return version.Errors;
        }

        var placement = await PlaceAsync(
            request.CityId, request.StarRating, request.Latitude, request.Longitude, cancellationToken);

        if (placement.IsError)
        {
            return placement.Errors;
        }

        var gallery = ReadGallery(request.Images);

        if (gallery.IsError)
        {
            return gallery.Errors;
        }

        var linked = await ReadAmenitiesAsync(request.AmenityIds, cancellationToken);

        if (linked.IsError)
        {
            return linked.Errors;
        }

        var hotel = await hotelRepository.GetByIdAsync(id, cancellationToken);

        if (hotel is null)
        {
            return HotelErrors.NotFound;
        }

        concurrencyGuard.Expect(hotel, version.Value);

        var applied = Apply(hotel, request, placement.Value, gallery.Value);

        if (applied.IsError)
        {
            return applied.Errors;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        await InvalidateAsync(id, cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        logger.LogInformation(
            "Updated hotel {HotelId} with {ImageCount} images and {AmenityCount} amenities",
            id, hotel.Images.Count, hotel.AmenityIds.Count);

        return HotelDto.From(hotel, linked.Value, concurrencyGuard.TokenFor(hotel));
    }

    public async Task<Result<Deleted>> DeleteAsync(
        Guid id,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var version = ConcurrencyToken.Create(ifMatch);

        if (version.IsError)
        {
            return version.Errors;
        }

        var hotel = await hotelRepository.GetByIdAsync(id, cancellationToken);

        if (hotel is null)
        {
            return HotelErrors.NotFound;
        }

        if (await roomRepository.ExistsInHotelAsync(id, cancellationToken))
        {
            Telemetry.CatalogDeletesRefused.Add(1, new KeyValuePair<string, object?>("resource", "hotel"));

            logger.LogInformation("Refused to delete hotel {HotelId} because rooms still belong to it", id);

            return HotelErrors.HasRooms;
        }

        concurrencyGuard.Expect(hotel, version.Value);

        var deleted = hotel.Delete(dateTimeProvider.UtcNow);

        if (deleted.IsError)
        {
            return deleted.Errors;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        await InvalidateAsync(id, cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        logger.LogInformation("Deleted hotel {HotelId}", id);

        return Result.Deleted;
    }

    private Task InvalidateAsync(Guid id, CancellationToken cancellationToken) =>
        cacheService.RemoveAsync(HotelCacheKeys.Details(id), cancellationToken);

    private Result<Updated> Apply(
        Hotel hotel,
        UpdateHotelRequest request,
        Placement placement,
        IReadOnlyList<HotelImage> gallery)
    {
        var moved = hotel.MoveTo(request.CityId, dateTimeProvider.UtcNow);

        if (moved.IsError)
        {
            return moved.Errors;
        }

        var updated = hotel.Update(
            request.Name,
            request.Description,
            request.Owner,
            placement.StarRating,
            placement.Location,
            request.ThumbnailUrl,
            dateTimeProvider.UtcNow);

        if (updated.IsError)
        {
            return updated.Errors;
        }

        var regallery = hotel.ReplaceGallery(gallery, dateTimeProvider.UtcNow);

        if (regallery.IsError)
        {
            return regallery.Errors;
        }

        return hotel.LinkAmenities(request.AmenityIds ?? [], dateTimeProvider.UtcNow);
    }

    private static Result<List<HotelImage>> ReadGallery(IReadOnlyList<HotelImageRequest>? images)
    {
        if (images is null or { Count: 0 })
        {
            return new List<HotelImage>();
        }

        List<Error> errors = [];
        List<HotelImage> gallery = [];

        foreach (var created in images.Select(image => HotelImage.Create(image.Url, image.Caption)))
        {
            if (created.IsError)
            {
                errors.AddRange(created.Errors);
            }
            else
            {
                gallery.Add(created.Value);
            }
        }

        return errors.Count > 0 ? errors.Distinct().ToList() : gallery;
    }

    private async Task<Result<IReadOnlyList<AmenityDto>>> ReadAmenitiesAsync(
        IReadOnlyList<Guid>? amenityIds,
        CancellationToken cancellationToken)
    {
        if (amenityIds is null or { Count: 0 })
        {
            return Result<IReadOnlyList<AmenityDto>>.From([]);
        }

        List<Guid> wanted = [.. amenityIds.Distinct()];

        var linked = await amenityQueries.ListByIdsAsync(wanted, cancellationToken);

        return linked.Count == wanted.Count
            ? Result<IReadOnlyList<AmenityDto>>.From(linked)
            : AmenityErrors.UnknownAmenity;
    }

    private async Task<Result<Placement>> PlaceAsync(
        Guid cityId,
        int starRating,
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken)
    {
        var rating = StarRating.Create(starRating);
        var location = GeoLocation.Create(latitude, longitude);

        List<Error> errors = [.. rating.Errors, .. location.Errors];

        if (errors.Count > 0)
        {
            return errors;
        }

        return cityId != Guid.Empty && !await cityRepository.ExistsAsync(cityId, cancellationToken)
            ? HotelErrors.CityNotFound
            : new Placement(rating.Value, location.Value);
    }

    private sealed record Placement(StarRating StarRating, GeoLocation Location);
}
