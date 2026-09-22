using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Rooms;
using HotelBooking.Application.Rooms.Dtos;

using Microsoft.AspNetCore.Mvc;

namespace HotelBooking.Api.Endpoints.Rooms;

public sealed class UpdateRoomEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/rooms/{id:guid}", async (
                Guid id,
                UpdateRoomRequest request,
                [FromHeader(Name = "If-Match")] string? ifMatch,
                IRoomService roomService,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                var result = await roomService.UpdateAsync(id, request, ifMatch, cancellationToken);

                return result.Match(
                    room => VersionedResults.Ok(response, room, room.Version),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Rooms)
            .WithSummary("Edit a room")
            .WithDescription(
                "Replaces the room's number, type, capacity and nightly rate. Requires an "
                + "`If-Match` header carrying the version from the last read: absent it is `428`, "
                + "stale it is `409`. Repricing never touches bookings already made — their "
                + "totals were snapshotted at checkout.")
            .Produces<RoomDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization(Policy.AdminOnly);
}
