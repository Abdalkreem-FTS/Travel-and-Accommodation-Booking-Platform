using System.Diagnostics;

namespace HotelBooking.Api.Errors;

internal static class TraceContext
{
    internal const string ProblemDetailsMember = "traceId";

    internal static string TraceId(this HttpContext httpContext) =>
        Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
