using HotelBooking.Application;
using HotelBooking.Application.Abstractions;

using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace HotelBooking.Api.Authentication;

internal static class SessionRevocationCheck
{
    private const string RevokedMessage = "This access token has been revoked.";

    public static async Task RejectRevokedSessionsAsync(
        TokenValidatedContext context,
        ITokenDenylist tokenDenylist,
        ILogger logger)
    {
        if (context.Principal is not { } principal || !principal.TryGetSessionId(out var sessionId))
        {
            context.Fail(RevokedMessage);
            return;
        }

        try
        {
            if (await tokenDenylist.IsSessionDeniedAsync(sessionId, context.HttpContext.RequestAborted))
            {
                context.Fail(RevokedMessage);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HandleUnavailableDenylist(context, logger, exception);
        }
    }

    private static void HandleUnavailableDenylist(
        TokenValidatedContext context,
        ILogger logger,
        Exception exception)
    {
        var failClosed = !HttpMethods.IsGet(context.HttpContext.Request.Method)
                         && !HttpMethods.IsHead(context.HttpContext.Request.Method)
                         && !HttpMethods.IsOptions(context.HttpContext.Request.Method);

        Telemetry.DenylistUnavailable.Add(
            1,
            new KeyValuePair<string, object?>("operation", "check"),
            new KeyValuePair<string, object?>("outcome", failClosed ? "rejected" : "admitted"));

        logger.LogError(
            exception,
            "Session denylist unreachable; {Method} {Path} was {Outcome} without a revocation check",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path,
            failClosed ? "rejected" : "admitted");

        if (failClosed)
        {
            context.Fail(RevokedMessage);
        }
    }
}
