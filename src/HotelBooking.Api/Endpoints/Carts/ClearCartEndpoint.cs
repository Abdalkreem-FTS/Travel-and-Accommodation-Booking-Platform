using System.Security.Claims;

using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Carts;

namespace HotelBooking.Api.Endpoints.Carts;

public sealed class ClearCartEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/cart", async (
                ICartService cartService,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                var result = await cartService.ClearAsync(user.GetUserId(), cancellationToken);

                return result.Match(_ => Results.NoContent(), CustomResults.Problem);
            })
            .WithTags(Tags.Cart)
            .WithSummary("Empty the cart")
            .WithDescription(
                "Drops every stay the caller is holding. Emptying an already empty cart is still "
                + "`204`, so the call is safe to repeat.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(Policy.AuthenticatedUser);
}
