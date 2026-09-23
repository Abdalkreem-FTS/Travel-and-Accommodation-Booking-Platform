using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Users;

namespace HotelBooking.Api.Endpoints.Users;

public sealed class GrantUserRoleEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/users/{userId:guid}/roles/{role}", async (
                Guid userId,
                string role,
                IUserRoleService userRoleService,
                CancellationToken cancellationToken) =>
            {
                var result = await userRoleService.GrantAsync(userId, role, cancellationToken);

                return result.Match(_ => Results.NoContent(), CustomResults.Problem);
            })
            .WithTags(Tags.Users)
            .WithSummary("Grant a role to a user")
            .WithDescription(
                "Adds a role to a user, keeping the roles they already hold. `PUT` because the "
                + "outcome is a state, not an event: granting a role the user already has succeeds "
                + "and changes nothing, so a retry after a timeout is always safe.\n\n"
                + "The new role reaches the user's access token at their **next refresh**, within "
                + "one access-token lifetime. Their sessions are deliberately left alone — gaining "
                + "a privilege a few minutes late is a delay, while losing one late would be a "
                + "hole, which is why `DELETE` on this same URI behaves differently.\n\n"
                + "Two administrators granting the same role at the same instant resolve to one "
                + "winner and one `409 User.RoleGrantRaced`; the retry then succeeds as a no-op.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(Policy.AdminOnly);
}
