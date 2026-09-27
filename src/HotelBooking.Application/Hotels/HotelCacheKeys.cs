using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using HotelBooking.Application.Common;

namespace HotelBooking.Application.Hotels;

public static class HotelCacheKeys
{
    private const string DetailsPrefix = "hotel-details";

    private const string SearchPrefix = "hotel-search";

    public static readonly TimeSpan DetailsTimeToLive = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan SearchTimeToLive = TimeSpan.FromSeconds(90);

    public static CacheKey Details(Guid hotelId) =>
        new(DetailsPrefix, $"hotel:{hotelId:N}:details");

    public static CacheKey Search(HotelSearchCriteria criteria) =>
        new(SearchPrefix, $"hotels:search:{Fingerprint(criteria)}");

    private static string Fingerprint(HotelSearchCriteria criteria)
    {
        var canonical = string.Join('|', (string[])
        [
            criteria.CityId?.ToString("N") ?? "-",
            criteria.Stay is { } stay ? Number(stay.CheckIn.DayNumber) : "-",
            criteria.Stay is { } nights ? Number(nights.CheckOut.DayNumber) : "-",
            Number(criteria.Guests.Adults),
            Number(criteria.Guests.Children),
            criteria.MinPrice?.ToString(CultureInfo.InvariantCulture) ?? "-",
            criteria.MaxPrice?.ToString(CultureInfo.InvariantCulture) ?? "-",
            string.Join(',', criteria.Stars.Select(star => star.Value).Order()),
            criteria.RoomType?.ToString() ?? "-",
            criteria.Sort.ToString(),
            Number(criteria.Page),
            Number(criteria.PageSize)
        ]);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
