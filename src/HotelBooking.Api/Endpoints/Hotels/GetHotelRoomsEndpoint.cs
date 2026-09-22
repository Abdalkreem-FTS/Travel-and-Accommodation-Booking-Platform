using HotelBooking.Api.Errors;
using HotelBooking.Application.Common;
using HotelBooking.Application.Rooms;
using HotelBooking.Application.Rooms.Dtos;

namespace HotelBooking.Api.Endpoints.Hotels;

public sealed class GetHotelRoomsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/hotels/{hotelId:guid}/rooms", async (
                Guid hotelId,
                [AsParameters] HotelRoomsRequest request,
                IRoomAvailabilityService roomAvailabilityService,
                CancellationToken cancellationToken) =>
            {
                var result = await roomAvailabilityService.ListForHotelAsync(hotelId, request, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Rooms)
            .WithSummary("List a hotel's rooms for a stay")
            .WithDescription(
                "Lists the rooms of the hotel named in the path that can host the party, so an "
                + "unknown hotel is `404`. "
                + "\n\n"
                + "Give `checkIn` and `checkOut` and the list is narrowed to rooms still free for "
                + "every night of that stay, read from the same room-night ledger checkout writes "
                + "to — a room disappears the moment its booking commits, and a check-out day is "
                + "free for the next guest. Each room then also carries `nights` and `total`, the "
                + "stay priced by the same rule checkout charges. Omit both dates and the response "
                + "is every room with its nightly rate and no total, because there is no stay to "
                + "price. Half a stay is `400 Hotel.StayIncomplete` rather than a silent "
                + "\"any date\", and the dates are held to the same rules as checkout, so a past "
                + "check-in or a stay over 30 nights is refused here too. "
                + "\n\n"
                + "`adults` defaults to 2 and `children` to 0. Rooms come cheapest first, ties "
                + $"broken on the id, paged with `page` from 1 and `pageSize` up to "
                + $"{Pagination.MaximumPageSize}.")
            .Produces<PagedList<AvailableRoomDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();
}
