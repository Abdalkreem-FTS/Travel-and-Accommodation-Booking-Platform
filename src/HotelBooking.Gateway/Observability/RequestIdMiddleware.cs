using System.Diagnostics;

using Serilog.Context;

namespace HotelBooking.Gateway.Observability;

internal sealed class RequestIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Request-Id";

    private const string SpanTag = "correlation.id";

    private const string LogProperty = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = Guid.NewGuid().ToString("N");

        context.Request.Headers[HeaderName] = requestId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = requestId;

            return Task.CompletedTask;
        });

        Activity.Current?.SetTag(SpanTag, requestId);

        using (LogContext.PushProperty(LogProperty, requestId))
        using (LogContext.PushProperty("ClientIp", context.Connection.RemoteIpAddress?.ToString()))
        {
            await next(context);
        }
    }
}
