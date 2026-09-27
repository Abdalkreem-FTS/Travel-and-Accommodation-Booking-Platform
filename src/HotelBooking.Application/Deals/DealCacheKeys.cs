using System.Globalization;

using HotelBooking.Application.Common;

namespace HotelBooking.Application.Deals;

public static class DealCacheKeys
{
    private const string FeaturedPrefix = "deals-featured";

    public static readonly TimeSpan FeaturedTimeToLive = TimeSpan.FromMinutes(10);

    public static CacheKey Featured(int limit) =>
        new(FeaturedPrefix, $"deals:featured:{limit.ToString(CultureInfo.InvariantCulture)}");

    public static IEnumerable<CacheKey> AllFeatured() =>
        Enumerable
            .Range(
                FeaturedDealsCriteria.MinimumLimit,
                FeaturedDealsCriteria.MaximumLimit - FeaturedDealsCriteria.MinimumLimit + 1)
            .Select(Featured);
}
