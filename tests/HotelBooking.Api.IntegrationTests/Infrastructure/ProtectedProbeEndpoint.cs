using System.Security.Claims;
using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HotelBooking.Api.IntegrationTests.Infrastructure;

internal sealed class ProtectedProbeEndpoint : IEndpoint
{
    public const string Route = "/probe/whoami";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, (ClaimsPrincipal user) => Results.Ok(new ProbeResponse(
                user.GetUserId(),
                user.FindFirst("role")?.Value,
                user.GetJti())))
            .RequireAuthorization(Policy.AuthenticatedUser);
}

internal sealed record ProbeResponse(Guid UserId, string? Role, string? Jti);
