using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Scalar.AspNetCore;

namespace HotelBooking.Api.Documentation;

public static class OpenApiExtensions
{
    private const string DocumentName = "v1";

    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer(DescribeApi);
            options.AddDocumentTransformer(DescribeSecurity);
        });

        return services;
    }

    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(options => options
                .WithTitle("Hotel Booking API")
                .WithTheme(ScalarTheme.BluePlanet))
            .AllowAnonymous();

        return app;
    }

    private static Task DescribeApi(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Hotel Booking API",
            Version = DocumentName,
            Description =
                "Hotel booking platform. All non-2xx responses are RFC 9457 ProblemDetails carrying "
                + "an `errorCode` and the `traceId` of the request span."
        };

        return Task.CompletedTask;
    }

    private static Task DescribeSecurity(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);

        document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Access token from `POST /api/sessions`. Expires after 15 minutes; rotate with "
                + "`PUT /api/sessions/current`."
        };

        return Task.CompletedTask;
    }
}
