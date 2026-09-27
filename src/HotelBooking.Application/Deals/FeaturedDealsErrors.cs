using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Deals;

public static class FeaturedDealsErrors
{
    public static Error LimitOutOfRange => Error.Validation(
        "Deal.LimitOutOfRange",
        "limit",
        $"Ask for between {FeaturedDealsCriteria.MinimumLimit} and "
        + $"{FeaturedDealsCriteria.MaximumLimit} deals.");

    public static Error UnfeaturedUnavailable => Error.Validation(
        "Deal.UnfeaturedUnavailable",
        "featured",
        "Only featured deals are served; omit the filter or ask for featured=true.");
}
