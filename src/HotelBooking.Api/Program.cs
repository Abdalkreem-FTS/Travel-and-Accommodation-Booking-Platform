using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Documentation;
using HotelBooking.Api.Endpoints;
using HotelBooking.Api.Errors;
using HotelBooking.Api.Observability;
using HotelBooking.Api.RateLimiting;
using HotelBooking.Application;
using HotelBooking.Infrastructure;

using HotelBooking.Infrastructure.Diagnostics;
using HotelBooking.Infrastructure.Persistence;
using HotelBooking.Infrastructure.Persistence.Seeding;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Timeouts;

using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(ObservabilityExtensions.ConfigureSerilog, writeToProviders: true);

builder.AddApiObservability();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOutboxDispatcher(builder.Configuration);

builder.Services.AddNotifications(builder.Configuration);

builder.Services.AddRequestTimeouts(options =>
    options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(30) });

builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddApiAuthorization();
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddApiProblemDetails();
builder.Services.AddApiDocumentation();
builder.Services.AddEndpoints();

var app = builder.Build();

app.Use(async (context, next) =>
{
    using (LogContext.PushProperty("ClientIp", context.Connection.RemoteIpAddress?.ToString()))
    {
        await next(context);
    }
});

app.UseSerilogRequestLogging();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseRequestTimeouts();

app.UseApiDocumentation();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();

app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions { Predicate = check => check.Tags.Contains(HealthCheckNames.ReadyTag) })
    .AllowAnonymous();

app.MapEndpoints();

await app.Services.MigrateDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    await app.Services.SeedDevelopmentCatalogAsync();
}

await app.RunAsync();
