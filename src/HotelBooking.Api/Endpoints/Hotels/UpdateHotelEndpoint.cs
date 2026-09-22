using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Hotels;
using HotelBooking.Application.Hotels.Dtos;

using Microsoft.AspNetCore.Mvc;

namespace HotelBooking.Api.Endpoints.Hotels;

public sealed class UpdateHotelEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/hotels/{id:guid}", async (
                Guid id,
                UpdateHotelRequest request,
                [FromHeader(Name = "If-Match")] string? ifMatch,
                IHotelService hotelService,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                var result = await hotelService.UpdateAsync(id, request, ifMatch, cancellationToken);

                return result.Match(
                    hotel => VersionedResults.Ok(response, hotel, hotel.Version),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Hotels)
            .WithSummary("Edit a hotel")
            .WithDescription(
                "Replaces the hotel's details, including the city it belongs to, so this is also "
                + "how a hotel is moved. Requires an `If-Match` header carrying the version from "
                + "the last read: absent it is `428`, stale it is `409`, and the existing rooms "
                + "and bookings are untouched either way. "
                + "\n\n"
                + "This replaces the gallery and the amenity links rather than adding to them, so "
                + "an `images` or `amenityIds` left out clears that collection — send back what a "
                + "read returned, minus what is meant to go.")
            .Produces<HotelDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization(Policy.AdminOnly);
}
