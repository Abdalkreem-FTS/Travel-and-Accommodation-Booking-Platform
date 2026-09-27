using System.Security.Claims;

using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Users;

namespace HotelBooking.Api.Endpoints.Users;

public sealed class RevokeUserRoleEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/users/{userId:guid}/roles/{role}", async (
                Guid userId,
                string role,
                IUserRoleService userRoleService,
                ClaimsPrincipal principal,
                CancellationToken cancellationToken) =>
            {
                var result = await userRoleService.RevokeAsync(
                    userId, role, principal.GetUserId(), cancellationToken);

                return result.Match(_ => Results.NoContent(), CustomResults.Problem);
            })
            .WithTags(Tags.Users)
            .WithSummary("Revoke a role from a user")
            .WithDescription(
                "Removes a role from a user. Unlike granting, this **ends every session that user "
                + "has**: their refresh tokens are revoked and their live access tokens are "
                + "denylisted, so the role stops working on the next request rather than whenever "
                + "the token happened to expire. The user is signed out on every device and signs "
                + "back in with the roles they now hold.\n\n"
                + "A user must keep at least one role, so revoking their last one is "
                + "`409 User.LastRoleCannotBeRevoked`. Revoking a role the user does not hold is "
                + "`404 User.RoleNotGranted`.\n\n"
                + "You cannot revoke a role from **yourself** (`403 User.CannotChangeYourOwnRoles`) "
                + "— an administrator who demotes themselves cannot undo it. Ask another "
                + "administrator.\n\n"
                + "If the role is revoked but Redis cannot be reached to deny the live tokens, the "
                + "answer is `503 User.RoleRevocationIncomplete`: the sessions are already dead and "
                + "no new token can be minted, but tokens already issued keep the role until they "
                + "expire, within one access-token lifetime (15 minutes by default). The revocation "
                + "has already happened, so there is **nothing to retry**: a retry finds the role "
                + "gone (`404 User.RoleNotGranted`) and denies nothing more. Wait out the window.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(Policy.AdminOnly);
}
