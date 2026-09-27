using HotelBooking.Api.Errors;
using HotelBooking.Application.Authentication;
using HotelBooking.Application.Authentication.Dtos;

namespace HotelBooking.Api.Endpoints.Sessions;

public sealed class RefreshSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/sessions/current", async (
                RefreshSessionRequest request,
                IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                var result = await authService.RefreshAsync(request, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Sessions)
            .WithSummary("Rotate the current session")
            .WithDescription(
                "Consumes the presented refresh token and issues a new pair. Rotation is strict: a "
                + "token presented twice revokes every token in its family, so clients must make "
                + "refresh single-flight.")
            .Produces<SessionDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .AllowAnonymous();
}
