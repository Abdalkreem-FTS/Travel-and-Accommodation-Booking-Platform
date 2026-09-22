using System.Security.Claims;
using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Authentication;

namespace HotelBooking.Api.Endpoints.Sessions;

public sealed class DeleteSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/sessions/current", async (
                IAuthService authService,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                var result = await authService.LogoutAsync(user.GetSessionId(), cancellationToken);

                return result.Match(_ => Results.NoContent(), CustomResults.Problem);
            })
            .WithTags(Tags.Sessions)
            .WithSummary("Log out")
            .WithDescription(
                "Ends the session this access token belongs to: revokes every refresh token in its "
                + "family and denylists the token's jti until it expires. Idempotent — a repeat call "
                + "with the same token is answered 401, because the token it authenticates with is "
                + "already revoked.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(Policy.AuthenticatedUser);
}
