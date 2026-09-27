namespace HotelBooking.Application.Common;

public sealed record PagedList<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > Pagination.FirstPage;

    public bool HasNextPage => Page < TotalPages;
}
