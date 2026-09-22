using System.Security.Claims;

using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Carts;
using HotelBooking.Application.Carts.Dtos;

namespace HotelBooking.Api.Endpoints.Carts;

public sealed class AddCartItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/cart/items", async (
                AddCartItemRequest request,
                ICartService cartService,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                var result = await cartService.AddItemAsync(request, user.GetUserId(), cancellationToken);

                return result.Match(cart => Results.Created("/api/cart", cart), CustomResults.Problem);
            })
            .WithTags(Tags.Cart)
            .WithSummary("Hold a room for a stay")
            .WithDescription(
                "Adds one room over one date range to the caller's cart and returns the whole cart "
                + "priced. **Holding a room reserves nothing**: the nights are sold at checkout by "
                + "the inventory ledger, so a room held here can still be taken by someone else. "
                + "Adding the same room over the same nights twice updates that stay rather than "
                + "duplicating it, which makes a double-submitted add harmless. A cart holds at "
                + "most ten stays (`409 Cart.Full`), all priced in one currency "
                + "(`409 Cart.CurrencyMismatch`). If the cart store is unreachable the add is "
                + "refused `503` rather than accepted and lost.")
            .Produces<CartDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(Policy.AuthenticatedUser);
}
