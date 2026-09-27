using HotelBooking.Api.Errors;
using HotelBooking.Application.Amenities;
using HotelBooking.Application.Amenities.Dtos;

namespace HotelBooking.Api.Endpoints.Amenities;

public sealed class GetAmenitiesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/amenities", async (
                IAmenityService amenityService,
                CancellationToken cancellationToken) =>
            {
                var result = await amenityService.ListAsync(cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Amenities)
            .WithSummary("List the amenity catalogue")
            .WithDescription(
                "Every amenity a hotel can claim, by name. The catalogue is shared reference data, "
                + "so these ids are what an administrator sends back in a hotel's `amenityIds`. It "
                + "is small and changes rarely; it is served from the database for now, and the "
                + "cache it deserves arrives with the rest of the read-through caching.")
            .Produces<IReadOnlyList<AmenityDto>>()
            .AllowAnonymous();
}
