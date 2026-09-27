using System.Security.Claims;

using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Carts;
using HotelBooking.Application.Carts.Dtos;

namespace HotelBooking.Api.Endpoints.Carts;

public sealed class GetCartEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/cart", async (
                ICartService cartService,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                var result = await cartService.GetAsync(user.GetUserId(), cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Cart)
            .WithSummary("Read the caller's cart")
            .WithDescription(
                "The stays the caller is holding. There is one cart per caller, keyed by the token's "
                + "subject, so a cart is never addressable by anyone else. An empty cart is `200` "
                + "with no items, not `404`. Stays whose check-in has passed are dropped on read - a "
                + "cart lives 24 hours and outlives some of the nights in it. Prices are the quote "
                + "as it stood when each stay was added; checkout prices the stay again and holds "
                + "nothing until then. If the cart store is unreachable the response is an empty "
                + "cart with `degraded: true` rather than an error.")
            .Produces<CartDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization(Policy.AuthenticatedUser);
}
