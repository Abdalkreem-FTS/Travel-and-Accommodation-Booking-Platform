using HotelBooking.Api.Errors;
using HotelBooking.Application.Cities;
using HotelBooking.Application.Cities.Dtos;
using HotelBooking.Application.Common;

namespace HotelBooking.Api.Endpoints.Cities;

public sealed class ListCitiesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/cities", async (
                [AsParameters] CityListRequest request,
                ICityService cityService,
                CancellationToken cancellationToken) =>
            {
                var result = await cityService.ListAsync(request, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Cities)
            .WithSummary("List cities")
            .WithDescription(
                "Every city, by name, with the number of hotels in each — the count a delete is "
                + "refused for, so an administrator can see which cities are still referenced "
                + "without opening them one at a time. Each row carries the version its edit must "
                + "quote in `If-Match`, the same one `GET /cities/{id}` returns. "
                + "\n\n"
                + "`search` matches anywhere in the name and ignores case, so `amm` finds Amman. "
                + "Ties break on the id, so paging never drops or repeats a row. "
                + "\n\n"
                + $"Paged with `page` from 1 and `pageSize` up to {Pagination.MaximumPageSize}. "
                + "`sort=trending` ranks destinations by how often their hotels have been viewed, "
                + "newest counters included, and holds **only cities anybody has visited** — an "
                + "unvisited city is not trending and is absent rather than listed last. That "
                + "ranking lives in Redis, so `search` cannot be combined with it, and if the "
                + "counters are unreachable the sort is refused with `503` rather than answered "
                + "by name under the wrong heading.")
            .Produces<PagedList<CitySummaryDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .AllowAnonymous();
}
