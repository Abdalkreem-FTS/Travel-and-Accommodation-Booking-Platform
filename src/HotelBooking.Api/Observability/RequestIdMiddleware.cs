using System.Diagnostics;

using Serilog.Context;

namespace HotelBooking.Api.Observability;

internal sealed class RequestIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Request-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = requestId;

            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("ClientIp", context.Connection.RemoteIpAddress?.ToString()))
        {
            await next(context);
        }
    }
}
