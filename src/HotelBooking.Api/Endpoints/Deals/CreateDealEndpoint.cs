using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Deals;
using HotelBooking.Application.Deals.Dtos;

namespace HotelBooking.Api.Endpoints.Deals;

public sealed class CreateDealEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/deals", async (
                CreateDealRequest request,
                IDealService dealService,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                var result = await dealService.CreateAsync(request, cancellationToken);

                return result.Match(
                    deal => VersionedResults.Created(
                        response, $"{EndpointExtensions.RoutePrefix}/deals/{deal.Id}", deal, deal.Version),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Deals)
            .WithSummary("Add a deal")
            .WithDescription(
                "Discounts one room over a half-open window: the start date runs, the end date does "
                + "not. The deal takes its hotel from the room, so only `roomId` is given — a room "
                + "nothing answers to is `400 Deal.RoomNotFound`, a field error, because the deal "
                + "is the resource being created."
                + "\n\n"
                + "A room-night carries at most one deal. A window overlapping another deal on the "
                + "same room is `409 Deal.OverlapsExisting` rather than a second discount whose "
                + "effect on a price would depend on which row was read first. Abutting windows are "
                + "fine — a deal ending on the 20th and one starting on the 20th do not overlap."
                + "\n\n"
                + "Writing a deal drops every cached featured list, so the home page reflects it on "
                + "the next request rather than after the ten-minute TTL.")
            .Produces<DealDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(Policy.AdminOnly);
}
