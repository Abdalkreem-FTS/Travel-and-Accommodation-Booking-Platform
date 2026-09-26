using HotelBooking.Application.Payments;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelBooking.Infrastructure.Payments;

public sealed class UnfinishedPaymentsWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<UnfinishedPaymentsOptions> options,
    ILogger<UnfinishedPaymentsWorker> logger) : BackgroundService
{
    private readonly UnfinishedPaymentsOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Unfinished payments worker started, handling up to {BatchSize} payments and refunds every {Interval}",
            _options.BatchSize, _options.Interval);

        using var timer = new PeriodicTimer(_options.Interval);

        do
        {
            await RunAsync("overdue payments", (services, token) =>
                services.GetRequiredService<IPaymentService>().ExpireOverdueAsync(_options.BatchSize, token), stoppingToken);

            await RunAsync("pending refunds", (services, token) =>
                services.GetRequiredService<IPaymentRefundService>().SendPendingAsync(_options.BatchSize, token), stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));

        logger.LogInformation("Unfinished payments worker stopped");
    }

    private async Task RunAsync(
        string what,
        Func<IServiceProvider, CancellationToken, Task<int>> work,
        CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();

            var handled = await work(scope.ServiceProvider, stoppingToken);

            if (handled > 0)
            {
                logger.LogInformation("Unfinished payments worker handled {Count} {What}", handled, what);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unfinished payments worker failed on {What}", what);
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
