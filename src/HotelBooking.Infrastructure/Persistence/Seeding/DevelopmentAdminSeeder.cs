using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Users;
using HotelBooking.Application.Users.Dtos;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Users;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HotelBooking.Infrastructure.Persistence.Seeding;

public static class DevelopmentAdminSeeder
{
    private const string LoggerCategory = "HotelBooking.Infrastructure.Persistence.Seeding";

    private const string FirstName = "Abdalkreem";

    private const string LastName = "Bzoor";

    extension(IServiceProvider services)
    {
        public async Task SeedDevelopmentAdminAsync(
            string? email,
            string? password,
            CancellationToken cancellationToken = default)
        {
            await using var scope = services.CreateAsyncScope();

            var provider = scope.ServiceProvider;

            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogInformation("No development admin is configured, so none was seeded.");

                return;
            }

            var registered = await provider.GetRequiredService<IUserService>().RegisterAsync(
                new RegisterUserRequest(email, password, FirstName, LastName), cancellationToken);

            if (registered.IsError && registered.TopError != UserErrors.EmailAlreadyRegistered)
            {
                logger.LogWarning(
                    "The development admin was not seeded: {ErrorCode}.", registered.TopError.Code);

                return;
            }

            var user = await provider.GetRequiredService<IUserRepository>()
                .GetByEmailAsync(Email.Create(email).Value, cancellationToken);

            if (user is null)
            {
                logger.LogWarning("The development admin was registered but could not be read back.");

                return;
            }

            user.Grant(UserRole.Admin, provider.GetRequiredService<IDateTimeProvider>().UtcNow);

            var saved = await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

            if (saved.IsError)
            {
                logger.LogWarning(
                    "The development admin role was not granted: {ErrorCode}.", saved.TopError.Code);

                return;
            }

            if (registered.IsError)
            {
                logger.LogInformation(
                    "The development admin {UserId} was already registered; it holds the Admin role.", user.Id);

                return;
            }

            logger.LogInformation("Seeded user {UserId} as the development admin.", user.Id);
        }
    }
}
