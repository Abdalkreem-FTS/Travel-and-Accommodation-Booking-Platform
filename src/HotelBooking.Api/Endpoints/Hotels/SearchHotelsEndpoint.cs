using HotelBooking.Api.Errors;
using HotelBooking.Application.Common;
using HotelBooking.Application.Hotels;
using HotelBooking.Application.Hotels.Dtos;

namespace HotelBooking.Api.Endpoints.Hotels;

public sealed class SearchHotelsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/hotels", async (
                [AsParameters] HotelSearchRequest request,
                IHotelSearchService hotelSearchService,
                CancellationToken cancellationToken) =>
            {
                var result = await hotelSearchService.SearchAsync(request, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Hotels)
            .WithSummary("Search hotels")
            .WithDescription(
                "Lists hotels, filtered by any combination of city, stay dates, guests, price band, "
                + "star rating and room type. Omit every filter for a plain listing. `fromPrice` is "
                + "the cheapest room that matched *all* of the filters, so it is a price the caller "
                + "can actually book; a hotel with no such room is not in the list at all. "
                + "\n\n"
                + "Giving `checkIn` and `checkOut` filters by real availability, against the same "
                + "room-night ledger checkout writes to, so a sold room disappears the moment its "
                + "booking commits. Half a stay is refused rather than ignored. "
                + "\n\n"
                + $"Results are paged: `page` starts at 1 and `pageSize` may be up to "
                + $"{Pagination.MaximumPageSize}. The response carries `totalCount` and `totalPages` "
                + "so a client can draw page links. "
                + "\n\n"
                + "Results are cached for 90 seconds against the filters as they were understood, "
                + "not as they were spelled, so reordering them or spelling out a default reaches "
                + "the same page. A room sold or a hotel added inside that window can take until "
                + "it expires to show; checkout, not this list, is what decides a room is yours. "
                + "\n\n"
                + "`amenities` and multi-room (`rooms`) filtering are not served yet — there is no "
                + "amenity catalogue, and a booking holds a single room.")
            .Produces<PagedList<HotelSummaryDto>>()
            .ProducesValidationProblem()
            .AllowAnonymous();
}
