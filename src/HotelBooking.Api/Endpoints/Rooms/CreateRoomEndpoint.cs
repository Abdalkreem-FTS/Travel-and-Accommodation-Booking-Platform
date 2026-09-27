using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Rooms;
using HotelBooking.Application.Rooms.Dtos;

namespace HotelBooking.Api.Endpoints.Rooms;

public sealed class CreateRoomEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/hotels/{hotelId:guid}/rooms", async (
                Guid hotelId,
                CreateRoomRequest request,
                IRoomService roomService,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                var result = await roomService.CreateAsync(hotelId, request, cancellationToken);

                return result.Match(
                    room => VersionedResults.Created(
                        response, $"{EndpointExtensions.RoutePrefix}/rooms/{room.Id}", room, room.Version),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Rooms)
            .WithSummary("Add a room to a hotel")
            .WithDescription(
                "Creates a room under the hotel named in the path, so an unknown hotel is `404`. "
                + "`type` is the `RoomType` ordinal and a value outside the enum is rejected "
                + "rather than stored. Room numbers are unique within a hotel, so a duplicate is "
                + "`409 Room.NumberAlreadyUsedInHotel`. The room is addressable at `/rooms/{id}`.")
            .Produces<RoomDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(Policy.AdminOnly);
}
