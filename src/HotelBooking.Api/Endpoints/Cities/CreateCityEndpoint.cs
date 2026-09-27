using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Cities;
using HotelBooking.Application.Cities.Dtos;

namespace HotelBooking.Api.Endpoints.Cities;

public sealed class CreateCityEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/cities", async (
                CreateCityRequest request,
                ICityService cityService,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                var result = await cityService.CreateAsync(request, cancellationToken);

                return result.Match(
                    city => VersionedResults.Created(
                        response, $"{EndpointExtensions.RoutePrefix}/cities/{city.Id}", city, city.Version),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Cities)
            .WithSummary("Add a city")
            .WithDescription(
                "Creates a city. Names are unique per country, enforced by a filtered unique index, "
                + "so a duplicate is `409 City.NameAlreadyUsedInCountry` rather than a second row "
                + "that looks identical. The response carries the row's version in both the `ETag` "
                + "header and the body, which is what a later edit must echo back in `If-Match`.")
            .Produces<CityDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(Policy.AdminOnly);
}
