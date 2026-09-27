using System.Diagnostics;

namespace HotelBooking.Gateway.Problems;

public static class GatewayProblems
{
    private const string TooManyRequestsCode = "Request.TooManyRequests";

    private const string UpstreamUnavailableCode = "Gateway.UpstreamUnavailable";

    public static IServiceCollection AddGatewayProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Customize);

    private static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var request = context.HttpContext.Request;

        problem.Type = null;
        problem.Instance ??= request.Path.HasValue ? request.Path.Value : "/";
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToHexString() ?? context.HttpContext.TraceIdentifier;

        if (problem.Extensions.ContainsKey("errorCode"))
        {
            return;
        }

        (string Code, string Title, string Detail)? known = problem.Status switch
        {
            StatusCodes.Status429TooManyRequests => (
                TooManyRequestsCode,
                "Too many requests",
                "Too many attempts. Wait for the period given in the Retry-After header and try again."),
            StatusCodes.Status502BadGateway
                or StatusCodes.Status503ServiceUnavailable
                or StatusCodes.Status504GatewayTimeout => (
                    UpstreamUnavailableCode,
                    "Upstream unavailable",
                    "No API instance could serve the request. Try again shortly."),
            _ => null
        };

        if (known is not { } error)
        {
            return;
        }

        problem.Extensions["errorCode"] = error.Code;
        problem.Title = error.Title;
        problem.Detail = error.Detail;
    }
}
