using HotelBooking.Api.Errors;
using HotelBooking.Api.RateLimiting;
using HotelBooking.Application.Authentication;
using HotelBooking.Application.Authentication.Dtos;
using Microsoft.AspNetCore.RateLimiting;

namespace HotelBooking.Api.Endpoints.Sessions;

public sealed class CreateSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/sessions", async (
                LoginRequest request,
                IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                var result = await authService.LoginAsync(request, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Sessions)
            .WithSummary("Log in")
            .WithDescription(
                "Exchanges credentials for an access token and a rotating refresh token. An unknown "
                + "email and a wrong password are answered identically, by design.")
            .Produces<SessionDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .AllowAnonymous();
}
