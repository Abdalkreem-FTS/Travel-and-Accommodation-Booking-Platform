
using Microsoft.AspNetCore.Authorization;

namespace HotelBooking.Api.Authorization;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy(
                Policy.AuthenticatedUser,
                policy => policy.RequireAuthenticatedUser())
            .AddPolicy(
                Policy.AdminOnly,
                policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin));

        return services;
    }
}
