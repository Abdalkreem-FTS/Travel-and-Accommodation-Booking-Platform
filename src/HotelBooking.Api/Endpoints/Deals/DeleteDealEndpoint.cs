using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Deals;

using Microsoft.AspNetCore.Mvc;

namespace HotelBooking.Api.Endpoints.Deals;

public sealed class DeleteDealEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/deals/{id:guid}", async (
                Guid id,
                [FromHeader(Name = "If-Match")] string? ifMatch,
                IDealService dealService,
                CancellationToken cancellationToken) =>
            {
                var result = await dealService.DeleteAsync(id, ifMatch, cancellationToken);

                return result.Match(_ => Results.NoContent(), CustomResults.Problem);
            })
            .WithTags(Tags.Deals)
            .WithSummary("Retire a deal")
            .WithDescription(
                "Soft-deletes a deal and drops it from the home page. Nothing blocks the delete: a "
                + "booking snapshots what it paid, so retiring a deal cannot change a price a guest "
                + "has already been quoted and charged."
                + "\n\n"
                + "The nights it covered are free for another deal immediately. Requires `If-Match`.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization(Policy.AdminOnly);
}
