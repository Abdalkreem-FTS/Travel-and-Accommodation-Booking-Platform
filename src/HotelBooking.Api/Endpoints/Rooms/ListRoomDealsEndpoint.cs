using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Common;
using HotelBooking.Application.Deals;
using HotelBooking.Application.Deals.Dtos;

namespace HotelBooking.Api.Endpoints.Rooms;

public sealed class ListRoomDealsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/rooms/{roomId:guid}/deals", async (
                Guid roomId,
                [AsParameters] RoomDealsRequest request,
                IDealService dealService,
                CancellationToken cancellationToken) =>
            {
                var result = await dealService.ListForRoomAsync(roomId, request, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Deals)
            .WithSummary("List a room's deals")
            .WithDescription(
                "Every deal on the room: ended, running and upcoming, featured or not. `GET /deals` "
                + "shows only featured deals running today. Removed deals are left out, and an "
                + "unknown or removed room is `404`. Each row carries the version its edit or delete "
                + "must quote in `If-Match`, the same one `GET /deals/{id}` returns. "
                + "\n\n"
                + "Latest start first; deals starting the same day list the longer one first, then "
                + "break ties on the id, so paging never drops or repeats a row. Paged with `page` "
                + $"from 1 and `pageSize` up to {Pagination.MaximumPageSize} "
                + $"(default {Pagination.DefaultPageSize}).")
            .Produces<PagedList<DealDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(Policy.AdminOnly);
}
