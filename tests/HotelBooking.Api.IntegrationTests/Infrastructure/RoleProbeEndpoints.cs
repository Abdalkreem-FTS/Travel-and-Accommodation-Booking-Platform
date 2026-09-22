using System.Security.Claims;
using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HotelBooking.Api.IntegrationTests.Infrastructure;

internal sealed class AdminProbeEndpoint : IEndpoint
{
    public const string Route = "/probe/admin";

    private const string RoleClaim = "role";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, (ClaimsPrincipal user) => Results.Ok(new ProbeResponse(
                user.GetUserId(),
                user.FindFirst(RoleClaim)?.Value,
                user.GetJti())))
            .RequireAuthorization(Policy.AdminOnly);
}
