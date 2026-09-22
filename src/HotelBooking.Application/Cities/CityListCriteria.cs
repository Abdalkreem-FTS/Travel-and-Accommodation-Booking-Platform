using HotelBooking.Application.Cities.Dtos;
using HotelBooking.Application.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Cities;

public sealed record CityListCriteria(string? Search, CitySort Sort, int Page, int PageSize)
{
    public int Skip => (Page - Pagination.FirstPage) * PageSize;

    public static Result<CityListCriteria> Create(CityListRequest request)
    {
        List<Error> errors = [];

        var sort = ParseSort(request.Sort, errors);
        var paging = PageRequest.Read(request.Page, request.PageSize, errors);
        var search = Normalise(request.Search);

        if (sort is CitySort.Trending && search is not null)
        {
            errors.Add(CityListErrors.TrendingSearchUnsupported);
        }

        return errors.Count > 0
            ? errors
            : new CityListCriteria(search, sort, paging.Page, paging.PageSize);
    }

    private static string? Normalise(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    private static CitySort ParseSort(string? sort, List<Error> errors)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return CitySort.Name;
        }

        if (Enum.TryParse<CitySort>(sort, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        errors.Add(CityListErrors.SortUnknown);

        return CitySort.Name;
    }
}
