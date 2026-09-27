using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Hotels;
using HotelBooking.Application.Hotels.Dtos;

namespace HotelBooking.Api.Endpoints.Hotels;

public sealed class CreateHotelEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/hotels", async (
                CreateHotelRequest request,
                IHotelService hotelService,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                var result = await hotelService.CreateAsync(request, cancellationToken);

                return result.Match(
                    hotel => VersionedResults.Created(
                        response, $"{EndpointExtensions.RoutePrefix}/hotels/{hotel.Id}", hotel, hotel.Version),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Hotels)
            .WithSummary("Add a hotel")
            .WithDescription(
                "Creates a hotel in an existing city. A `cityId` no city answers to is a field "
                + "error on the request rather than a 404, because the hotel is what is being "
                + "created here. Names are unique within a city, so a duplicate is "
                + "`409 Hotel.NameAlreadyUsedInCity`. "
                + "\n\n"
                + "`images` is the gallery in the order it should be shown, and `amenityIds` names "
                + "entries of the shared amenity catalogue — an id no amenity answers to is a field "
                + "error for the same reason `cityId` is. Both may be omitted for a hotel that has "
                + "neither yet.")
            .Produces<HotelDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(Policy.AdminOnly);
}
