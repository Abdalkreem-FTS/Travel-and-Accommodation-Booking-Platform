using HotelBooking.Application.Common;
using HotelBooking.Application.Hotels.Dtos;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

namespace HotelBooking.Application.Hotels;

public sealed record HotelSearchCriteria(
    Guid? CityId,
    DateRange? Stay,
    Occupancy Guests,
    decimal? MinPrice,
    decimal? MaxPrice,
    IReadOnlyList<StarRating> Stars,
    RoomType? RoomType,
    HotelSort Sort,
    int Page,
    int PageSize)
{
    private const int DefaultAdults = 2;

    private const int DefaultChildren = 0;

    public int Skip => (Page - Pagination.FirstPage) * PageSize;

    public static Result<HotelSearchCriteria> Create(HotelSearchRequest request, DateOnly today)
    {
        List<Error> errors = [];

        var sort = ParseSort(request.Sort, errors);
        var roomType = ParseRoomType(request.RoomType, errors);
        var stay = StayWindow.Read(request.CheckIn, request.CheckOut, today, errors);
        var stars = ReadStars(request.Stars, errors);
        var paging = PageRequest.Read(request.Page, request.PageSize, errors);

        var guests = Occupancy.Create(
            request.Adults ?? DefaultAdults, request.Children ?? DefaultChildren);

        errors.AddRange(guests.Errors);

        ReadPriceBand(request, errors);

        return errors.Count > 0
            ? errors
            : new HotelSearchCriteria(
                request.CityId,
                stay,
                guests.Value,
                request.MinPrice,
                request.MaxPrice,
                stars,
                roomType,
                sort,
                paging.Page,
                paging.PageSize);
    }

    private static HotelSort ParseSort(string? sort, List<Error> errors)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return HotelSort.Price;
        }

        if (Enum.TryParse<HotelSort>(sort, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        errors.Add(HotelSearchErrors.SortUnknown);

        return HotelSort.Price;
    }

    private static RoomType? ParseRoomType(string? roomType, List<Error> errors)
    {
        if (string.IsNullOrWhiteSpace(roomType))
        {
            return null;
        }

        if (Enum.TryParse<RoomType>(roomType, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        errors.Add(HotelSearchErrors.RoomTypeUnknown);

        return null;
    }

    private static List<StarRating> ReadStars(int[]? stars, List<Error> errors)
    {
        if (stars is null or { Length: 0 })
        {
            return [];
        }

        List<StarRating> ratings = [];

        foreach (var value in stars.Distinct())
        {
            var rating = StarRating.Create(value);

            if (rating.IsError)
            {
                errors.AddRange(rating.Errors);
            }
            else
            {
                ratings.Add(rating.Value);
            }
        }

        return ratings;
    }

    private static void ReadPriceBand(HotelSearchRequest request, List<Error> errors)
    {
        if (request.MinPrice < 0m || request.MaxPrice < 0m)
        {
            errors.Add(HotelSearchErrors.PriceNegative);
        }
        else if (request is { MinPrice: { } min, MaxPrice: { } max } && min > max)
        {
            errors.Add(HotelSearchErrors.PriceRangeInverted);
        }
    }
}
