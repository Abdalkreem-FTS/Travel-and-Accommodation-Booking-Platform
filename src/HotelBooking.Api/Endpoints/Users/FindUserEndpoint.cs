using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Users;
using HotelBooking.Application.Users.Dtos;

namespace HotelBooking.Api.Endpoints.Users;

public sealed class FindUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/users", async (
                [AsParameters] UserSearchRequest request,
                IUserService userService,
                CancellationToken cancellationToken) =>
            {
                var result = await userService.FindAsync(request, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Users)
            .WithSummary("Find a user by email")
            .WithDescription(
                "The user whose email is `email`, so an administrator can turn an address into the "
                + "id that `PUT` and `DELETE /users/{userId}/roles/{role}` take. The match is exact "
                + "and ignores case and surrounding spaces, the same way login reads an address. "
                + "An email names one user at most, so this returns that user, and no match is "
                + "`404 User.NotFound`. `email` is required. The address travels in the query "
                + "string rather than the path, which request logs record.")
            .Produces<UserDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(Policy.AdminOnly);
}
