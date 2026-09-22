using System.Security.Claims;

using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Hotels.Dtos;
using HotelBooking.Application.Visits;

namespace HotelBooking.Api.Endpoints.Visits;

public sealed class GetViewedHotelsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/viewed-hotels", async (
                IVisitService visitService,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                var result = await visitService.GetRecentlyViewedAsync(
                    user.GetUserId(), cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Hotels)
            .WithSummary("List the hotels the caller last viewed")
            .WithDescription(
                $"The last {VisitLimits.RecentHotelsKept} hotels this caller opened, newest first, "
                + "as the same cards the hotels collection returns. Viewing a hotel again moves it "
                + "to the front rather than listing it twice."
                + "\n\n"
                + "The list is keyed by the token's subject, so one guest's history is not "
                + "addressable by another, and only signed-in views are recorded — reading a hotel "
                + "anonymously counts towards that city's ranking and nothing else."
                + "\n\n"
                + "Having viewed nothing is an empty list, not a `404`. A hotel deleted since it "
                + "was viewed drops out silently. If the store is unreachable the answer is an "
                + "empty list rather than an error: a home page missing a strip still works.")
            .Produces<IReadOnlyList<HotelSummaryDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization(Policy.AuthenticatedUser);
}
