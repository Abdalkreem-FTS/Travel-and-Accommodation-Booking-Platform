

namespace HotelBooking.Api.Errors;

public static class ProblemDetailsServiceExtensions
{
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Customize);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    private static void Customize(ProblemDetailsContext context)
    {
        var request = context.HttpContext.Request;

        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceId();

        context.ProblemDetails.Instance ??= request.Path.HasValue ? request.Path.Value : "/";

        context.ProblemDetails.Type = null;

        AddFrameworkErrorCode(context);
    }

    private static void AddFrameworkErrorCode(ProblemDetailsContext context)
    {
        if (context.ProblemDetails.Extensions.ContainsKey("errorCode"))
        {
            return;
        }

        if (RequestErrors.ForFrameworkStatus(context.ProblemDetails.Status) is not { } error)
        {
            return;
        }

        context.ProblemDetails.Extensions["errorCode"] = error.Code;
        context.ProblemDetails.Detail ??= error.Description;
        context.ProblemDetails.Title = ErrorProblemMapping.Title(error);
    }
}
