using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Rooms;

using Microsoft.AspNetCore.Mvc;

namespace HotelBooking.Api.Endpoints.Rooms;

public sealed class DeleteRoomEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/rooms/{id:guid}", async (
                Guid id,
                [FromHeader(Name = "If-Match")] string? ifMatch,
                IRoomService roomService,
                CancellationToken cancellationToken) =>
            {
                var result = await roomService.DeleteAsync(id, ifMatch, cancellationToken);

                return result.Match(_ => Results.NoContent(), CustomResults.Problem);
            })
            .WithTags(Tags.Rooms)
            .WithSummary("Delete a room")
            .WithDescription(
                "Soft-deletes a room that owes nobody a night. The inventory ledger decides: a "
                + "room with nights sold today or later is `409 Room.HasFutureBookings`, while one "
                + "whose stays are all in the past can be retired, which keeps those bookings "
                + "readable as history. Requires `If-Match`.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization(Policy.AdminOnly);
}
