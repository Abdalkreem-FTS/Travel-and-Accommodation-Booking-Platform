using HotelBooking.Api.Errors;
using HotelBooking.Application.Rooms;
using HotelBooking.Application.Rooms.Dtos;

namespace HotelBooking.Api.Endpoints.Rooms;

public sealed class GetRoomEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/rooms/{id:guid}", async (
                Guid id,
                IRoomService roomService,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                var result = await roomService.GetAsync(id, cancellationToken);

                return result.Match(
                    room => VersionedResults.Ok(response, room, room.Version),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Rooms)
            .WithSummary("Read a room")
            .WithDescription(
                "Returns one room, its capacity, its nightly rate and the version an administrator "
                + "needs before editing it. Availability for a stay is a question for the hotel's "
                + "rooms collection, which takes the dates.")
            .Produces<RoomDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();
}
