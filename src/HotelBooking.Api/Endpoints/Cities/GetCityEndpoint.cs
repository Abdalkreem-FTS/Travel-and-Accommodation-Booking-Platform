using HotelBooking.Api.Errors;
using HotelBooking.Application.Cities;
using HotelBooking.Application.Cities.Dtos;

namespace HotelBooking.Api.Endpoints.Cities;

public sealed class GetCityEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/cities/{id:guid}", async (
                Guid id,
                ICityService cityService,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                var result = await cityService.GetAsync(id, cancellationToken);

                return result.Match(
                    city => VersionedResults.Ok(response, city, city.Version),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Cities)
            .WithSummary("Read a city")
            .WithDescription(
                "Returns one city and the version an administrator needs before editing it. A "
                + "deleted city is `404`: the soft delete hides the row from every read path, so "
                + "there is no state in which a client sees a city it may not act on.")
            .Produces<CityDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();
}
