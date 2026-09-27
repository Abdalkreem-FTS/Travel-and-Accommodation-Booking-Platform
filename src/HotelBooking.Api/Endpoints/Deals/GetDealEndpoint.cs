using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Deals;
using HotelBooking.Application.Deals.Dtos;

namespace HotelBooking.Api.Endpoints.Deals;

public sealed class GetDealEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/deals/{id:guid}", async (
                Guid id,
                IDealService dealService,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                var result = await dealService.GetAsync(id, cancellationToken);

                return result.Match(
                    deal => VersionedResults.Ok(response, deal, deal.Version),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Deals)
            .WithSummary("Read one deal")
            .WithDescription(
                "One deal as its administrator sees it, priced in percentage points rather than "
                + "money: what it costs a guest depends on the room, and `GET /deals` is where "
                + "that is worked out."
                + "\n\n"
                + "This is the read an edit starts from — the `ETag` it returns is what `If-Match` "
                + "wants. A deleted deal is `404`, not a readable tombstone.")
            .Produces<DealDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(Policy.AdminOnly);
}
