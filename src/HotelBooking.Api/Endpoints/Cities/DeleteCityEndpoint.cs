using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Cities;

using Microsoft.AspNetCore.Mvc;

namespace HotelBooking.Api.Endpoints.Cities;

public sealed class DeleteCityEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/cities/{id:guid}", async (
                Guid id,
                [FromHeader(Name = "If-Match")] string? ifMatch,
                ICityService cityService,
                CancellationToken cancellationToken) =>
            {
                var result = await cityService.DeleteAsync(id, ifMatch, cancellationToken);

                return result.Match(_ => Results.NoContent(), CustomResults.Problem);
            })
            .WithTags(Tags.Cities)
            .WithSummary("Delete a city")
            .WithDescription(
                "Soft-deletes a city that nothing references. A city that still has hotels is "
                + "`409 City.HasHotels`: deleting it would leave those hotels pointing at a row no "
                + "read path can see. Move or delete them first. Requires `If-Match`.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization(Policy.AdminOnly);
}
