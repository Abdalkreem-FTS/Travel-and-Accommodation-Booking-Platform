using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Cities;

public static class CityListErrors
{
    public static Error SortUnknown => Error.Validation(
        "City.SortUnknown", "sort", "Sort cities by name or trending.");

    public static Error TrendingSearchUnsupported => Error.Validation(
        "City.TrendingSearchUnsupported",
        "search",
        "Trending destinations cannot be searched; drop the search or sort by name.");

    public static Error TrendingUnavailable => Error.Unavailable(
        "City.TrendingUnavailable",
        "Visit counters are unreachable, so there is no ranking to serve. Sorting by name still works.");
}
