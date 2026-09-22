using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HotelBooking.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    private const string LoggerCategory = "HotelBooking.Infrastructure.Persistence.Migrations";

    extension(IServiceProvider services)
    {
        public async Task MigrateDatabaseAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = services.CreateAsyncScope();

            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(LoggerCategory);

            var database = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>().Database;

            var pending = (await database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

            if (pending.Length == 0)
            {
                logger.LogInformation("The database schema is up to date.");

                return;
            }

            logger.LogInformation("Applying {PendingCount} pending migration(s).", pending.Length);

            await database.MigrateAsync(cancellationToken);

            logger.LogInformation("Applied {PendingCount} migration(s).", pending.Length);
        }
    }
}
