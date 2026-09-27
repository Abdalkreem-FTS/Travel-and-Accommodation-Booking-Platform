using HotelBooking.Application.Abstractions;
using HotelBooking.Domain.Abstractions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HotelBooking.Infrastructure.Persistence.Seeding;

public static class DevelopmentCatalogSeeder
{
    private const string LoggerCategory = "HotelBooking.Infrastructure.Persistence.Seeding";

    extension(IServiceProvider services)
    {
        public async Task SeedDevelopmentCatalogAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = services.CreateAsyncScope();

            var provider = scope.ServiceProvider;

            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
            var context = provider.GetRequiredService<HotelBookingDbContext>();

            if (await context.Cities.IgnoreQueryFilters().AnyAsync(cancellationToken))
            {
                logger.LogInformation("The development catalog is already seeded.");

                return;
            }

            var catalog = CatalogSeedData.Build(provider.GetRequiredService<IDateTimeProvider>().UtcNow);

            context.Cities.AddRange(catalog.Cities);
            context.Hotels.AddRange(catalog.Hotels);
            context.Rooms.AddRange(catalog.Rooms);
            context.Deals.AddRange(catalog.Deals);

            var (cities, hotels, rooms, deals) = (
                catalog.Cities.Count, catalog.Hotels.Count, catalog.Rooms.Count, catalog.Deals.Count);

            var saved = await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

            if (saved.IsError)
            {
                logger.LogWarning(
                    "The development catalog seed was not written: {ErrorCode}.",
                    saved.TopError.Code);

                return;
            }

            logger.LogInformation(
                "Seeded the development catalog: {CityCount} cities, {HotelCount} hotels, "
                + "{RoomCount} rooms, {DealCount} deals.",
                cities,
                hotels,
                rooms,
                deals);
        }
    }

}
