using System.Security.Claims;

using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Carts;

namespace HotelBooking.Api.Endpoints.Carts;

public sealed class RemoveCartItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/cart/items/{itemId}", async (
                string itemId,
                ICartService cartService,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                var result = await cartService.RemoveItemAsync(itemId, user.GetUserId(), cancellationToken);

                return result.Match(_ => Results.NoContent(), CustomResults.Problem);
            })
            .WithTags(Tags.Cart)
            .WithSummary("Drop one stay from the cart")
            .WithDescription(
                "Removes a single held stay by the id `GET /cart` gave for it. A stay the caller's "
                + "cart does not hold is `404 Cart.ItemNotFound` - the id is only ever looked for in "
                + "the caller's own cart, so one caller cannot probe or remove another's.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(Policy.AuthenticatedUser);
}
