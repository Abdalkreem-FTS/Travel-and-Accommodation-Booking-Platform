using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Common;

public static class PaginationErrors
{
    public static Error PageOutOfRange => Error.Validation(
        "Pagination.PageOutOfRange",
        "page",
        "Page numbers start at 1.");

    public static Error PageSizeOutOfRange => Error.Validation(
        "Pagination.PageSizeOutOfRange",
        "pageSize",
        $"Ask for between 1 and {Pagination.MaximumPageSize} records per page.");
}
