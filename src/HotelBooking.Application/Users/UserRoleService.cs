using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Authentication;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.RefreshTokens;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Users;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Users;

public sealed class UserRoleService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITokenDenylist tokenDenylist,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    AuthenticationOptions options,
    ILogger<UserRoleService> logger) : IUserRoleService
{
    public async Task<Result<Updated>> GrantAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsed))
        {
            return UserErrors.UnknownRole;
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return UserErrors.NotFound;
        }

        var granted = user.Grant(parsed, dateTimeProvider.UtcNow);

        if (granted.IsError)
        {
            return granted.Errors;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        Telemetry.UserRolesGranted.Add(1, new KeyValuePair<string, object?>("role", parsed.ToString()));

        logger.LogInformation("Granted role {Role} to user {UserId}", parsed, userId);

        return Result.Updated;
    }

    public async Task<Result<Deleted>> RevokeAsync(
        Guid userId,
        string role,
        Guid actingUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsed))
        {
            return UserErrors.UnknownRole;
        }

        if (userId == actingUserId)
        {
            return UserErrors.CannotChangeYourOwnRoles;
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return UserErrors.NotFound;
        }

        var revoked = user.Revoke(parsed);

        if (revoked.IsError)
        {
            return revoked.Errors;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        Telemetry.UserRolesRevoked.Add(1, new KeyValuePair<string, object?>("role", parsed.ToString()));

        return await EndEverySessionAsync(userId, parsed, cancellationToken);
    }

    private async Task<Result<Deleted>> EndEverySessionAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var sessionIds = await refreshTokenRepository.RevokeAllForUserAsync(
            userId, dateTimeProvider.UtcNow, cancellationToken);

        try
        {
            foreach (var sessionId in sessionIds)
            {
                await tokenDenylist.DenySessionAsync(
                    sessionId, options.SessionRevocationWindow, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Telemetry.DenylistUnavailable.Add(1, new KeyValuePair<string, object?>("operation", "deny"));

            logger.LogError(
                exception,
                "Revoked role {Role} from user {UserId} and ended {SessionCount} session(s), but "
                + "could not deny their access tokens; the role stays usable for up to {Window}",
                role,
                userId,
                sessionIds.Count,
                options.SessionRevocationWindow);

            return UserErrors.RoleRevocationIncomplete;
        }

        logger.LogInformation(
            "Revoked role {Role} from user {UserId}; ended {SessionCount} session(s)",
            role,
            userId,
            sessionIds.Count);

        return Result.Deleted;
    }
}
