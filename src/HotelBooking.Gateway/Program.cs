using HotelBooking.Gateway.Forwarding;
using HotelBooking.Gateway.Observability;
using HotelBooking.Gateway.Problems;
using HotelBooking.Gateway.RateLimiting;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddGatewayObservability();

builder.Services.AddGatewayProblemDetails();
builder.Services.AddGatewayRateLimiting(builder.Configuration);
builder.Services.AddGatewayProxy(builder.Configuration);
builder.Services.AddHealthChecks();

var reverseProxy = builder.Build();

reverseProxy.UseMiddleware<RequestIdMiddleware>();

reverseProxy.UseSerilogRequestLogging();

reverseProxy.UseExceptionHandler();
reverseProxy.UseStatusCodePages();

reverseProxy.UseRateLimiter();

reverseProxy.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .DisableRateLimiting();

reverseProxy.MapReverseProxy();

await reverseProxy.RunAsync();
