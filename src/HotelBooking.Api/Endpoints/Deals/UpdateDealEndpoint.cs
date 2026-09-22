using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Deals;
using HotelBooking.Application.Deals.Dtos;

using Microsoft.AspNetCore.Mvc;

namespace HotelBooking.Api.Endpoints.Deals;

public sealed class UpdateDealEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/deals/{id:guid}", async (
                Guid id,
                UpdateDealRequest request,
                [FromHeader(Name = "If-Match")] string? ifMatch,
                IDealService dealService,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                var result = await dealService.UpdateAsync(id, request, ifMatch, cancellationToken);

                return result.Match(
                    deal => VersionedResults.Ok(response, deal, deal.Version),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Deals)
            .WithSummary("Re-cut a deal")
            .WithDescription(
                "Replaces the discount, the window and whether the deal is featured. The room is "
                + "not editable: a discount on a different room is a different deal, and moving "
                + "one would silently retire the first. Create the new deal and delete this one."
                + "\n\n"
                + "The new window must still leave the room's nights free of other deals, so it can "
                + "be `409 Deal.OverlapsExisting`. Requires an `If-Match` header carrying the "
                + "version from the last read: without it the request is "
                + "`428 Concurrency.VersionRequired`, and if the row moved on since, it is "
                + "`409 Persistence.ConcurrencyConflict` instead of overwriting a colleague's edit.")
            .Produces<DealDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization(Policy.AdminOnly);
}
