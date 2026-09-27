using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Common;
using HotelBooking.Application.Deals.Dtos;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Deals;

public sealed class DealService(
    IDealQueries dealQueries,
    IDealRepository dealRepository,
    IRoomRepository roomRepository,
    ICacheService cacheService,
    IUnitOfWork unitOfWork,
    IConcurrencyGuard concurrencyGuard,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<DealService> logger) : IDealService
{
    public async Task<Result<DealDto>> CreateAsync(
        CreateDealRequest request,
        CancellationToken cancellationToken = default)
    {
        var discount = DiscountPercentage.Create(request.DiscountPercentage);

        if (discount.IsError)
        {
            return discount.Errors;
        }

        var room = await roomRepository.GetByIdAsync(request.RoomId, cancellationToken);

        if (room is null)
        {
            return DealErrors.RoomNotFound;
        }

        var deal = Deal.Create(
            guidProvider.NewSortable(),
            room.HotelId,
            room.Id,
            discount.Value,
            request.StartsOn,
            request.EndsOn,
            request.IsFeatured,
            dateTimeProvider.UtcNow);

        if (deal.IsError)
        {
            return deal.Errors;
        }

        dealRepository.Add(deal.Value);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            CountOverlap(saved.TopError);

            return saved.Errors;
        }

        await DropFeaturedListsAsync(cancellationToken);

        logger.LogInformation(
            "Created deal {DealId}: {DiscountPercentage}% off room {RoomId} from {StartsOn} to {EndsOn}",
            deal.Value.Id, deal.Value.Discount.Value, room.Id, deal.Value.StartsOn, deal.Value.EndsOn);

        return DealDto.From(deal.Value, concurrencyGuard.TokenFor(deal.Value));
    }

    public async Task<Result<DealDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deal = await dealRepository.GetByIdAsync(id, cancellationToken);

        return deal is null
            ? DealErrors.NotFound
            : DealDto.From(deal, concurrencyGuard.TokenFor(deal));
    }

    public async Task<Result<PagedList<DealDto>>> ListForRoomAsync(
        Guid roomId,
        RoomDealsRequest request,
        CancellationToken cancellationToken = default)
    {
        List<Error> errors = [];

        var paging = PageRequest.Read(request.Page, request.PageSize, errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        if (!await roomRepository.ExistsAsync(roomId, cancellationToken))
        {
            return RoomErrors.NotFound;
        }

        return await dealQueries.ListForRoomAsync(roomId, paging, cancellationToken);
    }

    public async Task<Result<DealDto>> UpdateAsync(
        Guid id,
        UpdateDealRequest request,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var version = ConcurrencyToken.Create(ifMatch);

        if (version.IsError)
        {
            return version.Errors;
        }

        var discount = DiscountPercentage.Create(request.DiscountPercentage);

        if (discount.IsError)
        {
            return discount.Errors;
        }

        var deal = await dealRepository.GetByIdAsync(id, cancellationToken);

        if (deal is null)
        {
            return DealErrors.NotFound;
        }

        var updated = deal.Update(
            discount.Value,
            request.StartsOn,
            request.EndsOn,
            request.IsFeatured,
            dateTimeProvider.UtcNow);

        if (updated.IsError)
        {
            return updated.Errors;
        }

        concurrencyGuard.Expect(deal, version.Value);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            CountOverlap(saved.TopError);

            return saved.Errors;
        }

        await DropFeaturedListsAsync(cancellationToken);

        logger.LogInformation("Updated deal {DealId}", id);

        return DealDto.From(deal, concurrencyGuard.TokenFor(deal));
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

        var deal = await dealRepository.GetByIdAsync(id, cancellationToken);

        if (deal is null)
        {
            return DealErrors.NotFound;
        }

        concurrencyGuard.Expect(deal, version.Value);

        var deleted = deal.Delete(dateTimeProvider.UtcNow);

        if (deleted.IsError)
        {
            return deleted.Errors;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        await DropFeaturedListsAsync(cancellationToken);

        logger.LogInformation("Deleted deal {DealId}", id);

        return Result.Deleted;
    }

    public async Task<Result<IReadOnlyList<FeaturedDealDto>>> ListFeaturedAsync(
        FeaturedDealsRequest request,
        CancellationToken cancellationToken = default)
    {
        var criteria = FeaturedDealsCriteria.Create(request);

        if (criteria.IsError)
        {
            return criteria.Errors;
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var deals = (await cacheService.GetOrSetAsync(
            DealCacheKeys.Featured(criteria.Value.Limit),
            async token => Price(await dealQueries.ListFeaturedAsync(criteria.Value.Limit, today, token)),
            DealCacheKeys.FeaturedTimeToLive,
            cancellationToken))!;

        logger.LogDebug("Served {DealCount} featured deals live on {Date}", deals.Count, today);

        return deals;
    }

    private void CountOverlap(Error failure)
    {
        if (failure != DealErrors.OverlapsExisting)
        {
            return;
        }

        Telemetry.DealOverlapsRefused.Add(1);

        logger.LogInformation(
            "Refused a deal write: another deal already holds one of those room-nights");
    }

    private async Task DropFeaturedListsAsync(CancellationToken cancellationToken)
    {
        foreach (var key in DealCacheKeys.AllFeatured())
        {
            await cacheService.RemoveAsync(key, cancellationToken);
        }
    }

    private List<FeaturedDealDto> Price(IReadOnlyList<FeaturedDealRow> rows) =>
        [.. rows.Select(Price).OfType<FeaturedDealDto>()];

    private FeaturedDealDto? Price(FeaturedDealRow row)
    {
        var discount = DiscountPercentage.Create(row.DiscountPercentage);
        var original = Money.Create(row.OriginalPrice, row.Currency);

        if (discount.IsError || original.IsError)
        {
            return Unpriceable(row);
        }

        var discounted = discount.Value.ApplyTo(original.Value);

        return discounted.IsError
            ? Unpriceable(row)
            : new FeaturedDealDto(
                row.Id,
                row.HotelId,
                row.HotelName,
                row.CityId,
                row.CityName,
                row.ThumbnailUrl,
                row.StarRating,
                row.RoomId,
                row.RoomType.ToString(),
                original.Value.Amount,
                discounted.Value.Amount,
                discount.Value.Value,
                original.Value.Currency,
                row.EndsOn);
    }

    private FeaturedDealDto? Unpriceable(FeaturedDealRow row)
    {
        Telemetry.DealsUnpriceable.Add(1);

        logger.LogWarning(
            "Deal {DealId} takes {DiscountPercentage}% off {OriginalPrice} {Currency} on room "
            + "{RoomId}, which is not a price the domain accepts, so it was left off the home page",
            row.Id,
            row.DiscountPercentage,
            row.OriginalPrice,
            row.Currency,
            row.RoomId);

        return null;
    }
}
