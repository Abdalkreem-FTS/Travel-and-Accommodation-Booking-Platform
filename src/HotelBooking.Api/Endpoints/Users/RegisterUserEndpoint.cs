using HotelBooking.Api.Errors;
using HotelBooking.Api.RateLimiting;
using HotelBooking.Application.Users;
using HotelBooking.Application.Users.Dtos;
using Microsoft.AspNetCore.RateLimiting;

namespace HotelBooking.Api.Endpoints.Users;

public sealed class RegisterUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/users", async (
                RegisterUserRequest request,
                IUserService userService,
                CancellationToken cancellationToken) =>
            {
                var result = await userService.RegisterAsync(request, cancellationToken);

                return result.Match(
                    user => Results.Created($"/api/users/{user.Id}", user),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Users)
            .WithSummary("Register a user")
            .WithDescription(
                "Creates an account. The role is always `User`; administrator access is granted out "
                + "of band and can never be requested here.")
            .Produces<UserDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .AllowAnonymous();
}
