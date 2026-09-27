using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Hotels;

using Microsoft.AspNetCore.Mvc;

namespace HotelBooking.Api.Endpoints.Hotels;

public sealed class DeleteHotelEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/hotels/{id:guid}", async (
                Guid id,
                [FromHeader(Name = "If-Match")] string? ifMatch,
                IHotelService hotelService,
                CancellationToken cancellationToken) =>
            {
                var result = await hotelService.DeleteAsync(id, ifMatch, cancellationToken);

                return result.Match(_ => Results.NoContent(), CustomResults.Problem);
            })
            .WithTags(Tags.Hotels)
            .WithSummary("Delete a hotel")
            .WithDescription(
                "Soft-deletes a hotel that has no rooms left. A hotel that still has rooms is "
                + "`409 Hotel.HasRooms`; since a room in turn refuses to go while it owes guests "
                + "nights, a hotel with live bookings can never be deleted out from under them. "
                + "Requires `If-Match`.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization(Policy.AdminOnly);
}
