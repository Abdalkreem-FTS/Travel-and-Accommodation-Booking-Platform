using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Documentation;
using HotelBooking.Api.Endpoints;
using HotelBooking.Api.Errors;
using HotelBooking.Api.Networking;
using HotelBooking.Api.Observability;
using HotelBooking.Application;
using HotelBooking.Infrastructure;

using HotelBooking.Infrastructure.Diagnostics;
using HotelBooking.Infrastructure.Persistence;
using HotelBooking.Infrastructure.Persistence.Seeding;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Timeouts;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddApiObservability();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddRequestTimeouts(options =>
    options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(30) });

builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddApiAuthorization();
builder.Services.AddGatewayForwardedHeaders();
builder.Services.AddApiProblemDetails();
builder.Services.AddApiDocumentation();
builder.Services.AddEndpoints();

var app = builder.Build();

app.UseForwardedHeaders();

app.UseMiddleware<RequestIdMiddleware>();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseRequestTimeouts();

app.UseApiDocumentation();

app.UseAuthentication();
app.UseAuthorization();

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
    await app.Services.SeedDevelopmentAdminAsync(app.Configuration["Seed:Admin:Email"], app.Configuration["Seed:Admin:Password"]);
}

await app.RunAsync();
