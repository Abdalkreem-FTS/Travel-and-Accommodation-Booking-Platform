using HotelBooking.Infrastructure;
using HotelBooking.Infrastructure.Observability;

var builder = Host.CreateApplicationBuilder(args);

builder.AddObservability();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWorkers(builder.Configuration);

var host = builder.Build();

await host.RunAsync();
