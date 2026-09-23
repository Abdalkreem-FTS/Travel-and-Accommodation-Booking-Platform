using FluentValidation;

using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Authentication.Dtos;
using HotelBooking.Application.Common;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.RefreshTokens;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Users;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Authentication;

public sealed class AuthService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IAccessTokenProvider accessTokenProvider,
    IRefreshTokenFactory refreshTokenFactory,
    AuthenticationOptions options,
    ITokenDenylist tokenDenylist,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    IValidator<LoginRequest> loginValidator,
    IValidator<RefreshSessionRequest> refreshValidator,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<Result<SessionDto>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = await loginValidator.ValidateToErrorsAsync(request, cancellationToken);

        if (validationErrors.Count > 0)
        {
            return validationErrors;
        }

        var email = Email.Create(request.Email);

        if (email.IsError)
        {
            return Reject("malformed_email");
        }

        var user = await userRepository.GetByEmailAsync(email.Value, cancellationToken);

        var verified = passwordHasher.Verify(user?.PasswordHash, request.Password);

        if (user is null || !verified)
        {
            return Reject(user is null ? "unknown_email" : "bad_password");
        }

        var refreshToken = refreshTokenFactory.Generate();
        var tokenId = guidProvider.NewSortable();
        var familyId = guidProvider.NewSortable();

        refreshTokenRepository.Add(RefreshToken.IssueForNewFamily(
            tokenId: tokenId,
            familyId: familyId,
            userId: user.Id,
            tokenHash: refreshToken.Hash,
            nowUtc: dateTimeProvider.UtcNow,
            lifetime: options.RefreshTokenLifetime));

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        Telemetry.LoginsSucceeded.Add(1);

        logger.LogInformation("User {UserId} logged in with session {SessionId}", user.Id, familyId);

        return IssueSession(user, familyId, refreshToken);

        Error Reject(string reason)
        {
            Telemetry.LoginsFailed.Add(1, new KeyValuePair<string, object?>("reason", reason));
            logger.LogInformation("Login rejected: {Reason}", reason);
            return AuthErrors.InvalidCredentials;
        }
    }

    public async Task<Result<SessionDto>> RefreshAsync(
        RefreshSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = await refreshValidator.ValidateToErrorsAsync(request, cancellationToken);

        if (validationErrors.Count > 0)
        {
            return validationErrors;
        }

        var presentedHash = refreshTokenFactory.Hash(request.RefreshToken);
        var token = await refreshTokenRepository.GetByTokenHashAsync(presentedHash, cancellationToken);

        if (token is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var replacement = refreshTokenFactory.Generate();
        var rotated = token.Rotate(
            replacementId: guidProvider.NewSortable(),
            newTokenHash: replacement.Hash,
            nowUtc: dateTimeProvider.UtcNow,
            lifetime: options.RefreshTokenLifetime);

        if (rotated.IsError)
        {
            return await HandleRotationFailureAsync(
                token, rotated.TopError, "replay", cancellationToken);
        }

        var user = await userRepository.GetByIdAsync(token.UserId, cancellationToken);

        if (user is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        refreshTokenRepository.Add(rotated.Value);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.TopError.Code == PersistenceErrors.ConcurrencyConflict.Code
                ? await HandleRotationFailureAsync(
                    token, RefreshTokenErrors.Reused, "race", cancellationToken)
                : saved.Errors;
        }

        return IssueSession(user, token.FamilyId, replacement);
    }

    public async Task<Result<Deleted>> LogoutAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var revoked = await refreshTokenRepository.RevokeFamilyAsync(sessionId, dateTimeProvider.UtcNow, cancellationToken);

        try
        {
            await tokenDenylist.DenySessionAsync(
                sessionId, options.SessionRevocationWindow, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Telemetry.DenylistUnavailable.Add(1, new KeyValuePair<string, object?>("operation", "deny"));

            logger.LogError(
                exception,
                "Logout revoked {RevokedCount} refresh token(s) for session {SessionId} but could not "
                + "deny its access tokens; they stay valid for up to {Window}",
                revoked,
                sessionId,
                options.SessionRevocationWindow);

            return RefreshTokenErrors.LogoutUnavailable;
        }

        Telemetry.SessionsEnded.Add(1);

        logger.LogInformation(
            "Session {SessionId} ended; revoked {RevokedCount} refresh token(s)", sessionId, revoked);

        return Result.Deleted;
    }

    private async Task<Result<SessionDto>> HandleRotationFailureAsync(
        RefreshToken token,
        Error error,
        string detection,
        CancellationToken cancellationToken)
    {
        if (error.Code != RefreshTokenErrors.Reused.Code)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var revoked = await refreshTokenRepository
            .RevokeFamilyAsync(token.FamilyId, dateTimeProvider.UtcNow, cancellationToken);

        await DenyTheSessionAsync(token.FamilyId, revoked, cancellationToken);

        Telemetry.RefreshTokensReused.Add(1, new KeyValuePair<string, object?>("detection", detection));

        logger.LogWarning(
            "Refresh token reuse detected ({Detection}) for user {UserId}; revoked {RevokedCount} "
            + "token(s) in family {FamilyId}",
            detection,
            token.UserId,
            revoked,
            token.FamilyId);

        return AuthErrors.InvalidRefreshToken;
    }

    private async Task DenyTheSessionAsync(
        Guid sessionId,
        int revoked,
        CancellationToken cancellationToken)
    {
        try
        {
            await tokenDenylist.DenySessionAsync(
                sessionId, options.SessionRevocationWindow, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Telemetry.DenylistUnavailable.Add(1, new KeyValuePair<string, object?>("operation", "deny"));

            logger.LogError(
                exception,
                "Reuse detection revoked {RevokedCount} token(s) in family {SessionId} but could not "
                + "deny its access tokens; they stay valid for up to {Window}",
                revoked,
                sessionId,
                options.SessionRevocationWindow);
        }
    }

    private SessionDto IssueSession(User user, Guid sessionId, GeneratedRefreshToken refreshToken)
    {
        var access = accessTokenProvider.Issue(user, sessionId);

        return new SessionDto(
            access.Value,
            access.ExpiresAtUtc,
            refreshToken.RawValue,
            dateTimeProvider.UtcNow.Add(options.RefreshTokenLifetime));
    }
}
