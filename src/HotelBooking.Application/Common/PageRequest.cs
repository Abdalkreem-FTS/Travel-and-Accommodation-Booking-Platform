using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Common;

public sealed record PageRequest(int Page, int PageSize)
{
    public int Skip => (Page - Pagination.FirstPage) * PageSize;

    public static PageRequest Read(int? page, int? pageSize, List<Error> errors) =>
        new(ReadPage(page, errors), ReadPageSize(pageSize, errors));

    private static int ReadPage(int? page, List<Error> errors)
    {
        switch (page)
        {
            case null:
                return Pagination.FirstPage;
            case >= Pagination.FirstPage:
                return page.Value;
            default:
                errors.Add(PaginationErrors.PageOutOfRange);

                return Pagination.FirstPage;
        }
    }

    private static int ReadPageSize(int? pageSize, List<Error> errors)
    {
        switch (pageSize)
        {
            case null:
                return Pagination.DefaultPageSize;
            case > 0 and <= Pagination.MaximumPageSize:
                return pageSize.Value;
            default:
                errors.Add(PaginationErrors.PageSizeOutOfRange);

                return Pagination.DefaultPageSize;
        }
    }
}
