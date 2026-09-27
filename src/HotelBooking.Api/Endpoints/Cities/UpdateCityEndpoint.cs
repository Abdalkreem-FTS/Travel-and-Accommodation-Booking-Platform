using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Cities;
using HotelBooking.Application.Cities.Dtos;

using Microsoft.AspNetCore.Mvc;

namespace HotelBooking.Api.Endpoints.Cities;

public sealed class UpdateCityEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/cities/{id:guid}", async (
                Guid id,
                UpdateCityRequest request,
                [FromHeader(Name = "If-Match")] string? ifMatch,
                ICityService cityService,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                var result = await cityService.UpdateAsync(id, request, ifMatch, cancellationToken);

                return result.Match(
                    city => VersionedResults.Ok(response, city, city.Version),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Cities)
            .WithSummary("Edit a city")
            .WithDescription(
                "Replaces the city's details. Requires an `If-Match` header carrying the version "
                + "from the last read: without it the request is `428 Concurrency.VersionRequired`, "
                + "and if the row moved on since, it is `409 Persistence.ConcurrencyConflict` "
                + "instead of quietly overwriting a colleague's edit. Reload and re-apply.")
            .Produces<CityDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization(Policy.AdminOnly);
}
