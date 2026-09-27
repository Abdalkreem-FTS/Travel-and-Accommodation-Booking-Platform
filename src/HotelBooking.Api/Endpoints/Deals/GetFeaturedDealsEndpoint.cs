using HotelBooking.Api.Errors;
using HotelBooking.Application.Deals;
using HotelBooking.Application.Deals.Dtos;

namespace HotelBooking.Api.Endpoints.Deals;

public sealed class GetFeaturedDealsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/deals", async (
                [AsParameters] FeaturedDealsRequest request,
                IDealService dealService,
                CancellationToken cancellationToken) =>
            {
                var result = await dealService.ListFeaturedAsync(request, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Deals)
            .WithSummary("List the featured deals")
            .WithDescription(
                "The discounts the home page leads with: the hotel, its city and thumbnail, the "
                + "room the deal applies to, and what that room costs with and without the "
                + "discount. Deepest discount first, at most ten, five by default."
                + "\n\n"
                + "Only featured deals are served — `featured=false` is refused rather than "
                + "quietly ignored, because a browse of every discount is a different list nobody "
                + "has asked for yet. A deal is included only while it is running: the start date "
                + "counts, the end date does not."
                + "\n\n"
                + "Served from a cache for up to ten minutes, so a deal that has just started or "
                + "just ended can take that long to appear or disappear — though an edit to a deal "
                + "drops the cache, so administrative changes show at once."
                + "\n\n"
                + "**The discounted price is what checkout charges.** `POST /bookings` prices each "
                + "night against the deals running on it, so booking a stay this deal only partly "
                + "covers is discounted for those nights and billed at the room's own rate for "
                + "the rest.")
            .Produces<IReadOnlyList<FeaturedDealDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();
}
