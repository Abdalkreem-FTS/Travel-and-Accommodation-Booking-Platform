using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Amenities;
using HotelBooking.Application.Authentication;
using HotelBooking.Application.Bookings;
using HotelBooking.Application.Cities;
using HotelBooking.Application.Deals;
using HotelBooking.Application.Hotels;
using HotelBooking.Application.Payments;
using HotelBooking.Application.Rooms;
using HotelBooking.Application.Visits;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Carts;
using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Idempotency;
using HotelBooking.Domain.RefreshTokens;
using HotelBooking.Domain.Rooms;
using HotelBooking.Domain.Users;
using HotelBooking.Infrastructure.Authentication;
using HotelBooking.Infrastructure.Caching;
using HotelBooking.Infrastructure.Diagnostics;
using HotelBooking.Infrastructure.Identifiers;
using HotelBooking.Infrastructure.Notifications;
using HotelBooking.Infrastructure.Outbox;
using HotelBooking.Infrastructure.Outbox.Handlers;
using HotelBooking.Infrastructure.Payments;
using HotelBooking.Infrastructure.Persistence;
using HotelBooking.Infrastructure.Persistence.Queries;
using HotelBooking.Infrastructure.Persistence.Repositories;
using HotelBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace HotelBooking.Infrastructure;

public static class DependencyInjection
{
    private const string DatabaseConnectionName = "Database";

    private const int CommandTimeoutSeconds = 10;

    extension(IServiceCollection services)
    {
        public IServiceCollection AddInfrastructure(IConfiguration configuration)
        {
            return services
                .AddPersistence(configuration)
                .AddCache(configuration)
                .AddAuthenticationServices(configuration)
                .AddHealthDiagnostics()
                .AddSingleton<IDateTimeProvider, SystemDateTimeProvider>()
                .AddSingleton<IGuidProvider, SystemGuidProvider>()
                .AddPayments(configuration);
        }

        private IServiceCollection AddPersistence(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString(DatabaseConnectionName)
                                   ?? throw new InvalidOperationException(
                                       $"Connection string '{DatabaseConnectionName}' is not configured.");

            services.AddSingleton<DrainDomainEventsInterceptor>();

            services.AddDbContext<HotelBookingDbContext>((provider, options) =>
                options
                    .AddInterceptors(provider.GetRequiredService<DrainDomainEventsInterceptor>())
                    .UseSqlServer(connectionString, sqlServer =>
                {
                    sqlServer.MigrationsAssembly(AssemblyReference.Assembly.FullName);

                    sqlServer.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);

                    sqlServer.CommandTimeout(CommandTimeoutSeconds);
                }));

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IConcurrencyGuard, EfConcurrencyGuard>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<ICityRepository, CityRepository>();
            services.AddScoped<ICityQueries, CityQueries>();
            services.AddScoped<IHotelRepository, HotelRepository>();
            services.AddScoped<IHotelQueries, HotelQueries>();
            services.AddScoped<IAmenityQueries, AmenityQueries>();
            services.AddScoped<IDealQueries, DealQueries>();
            services.AddScoped<IDealRepository, DealRepository>();
            services.AddScoped<IRoomQueries, RoomQueries>();
            services.AddScoped<IRoomRepository, RoomRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();
            services.AddScoped<IBookingQueries, BookingQueries>();
            services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
            services.AddScoped<IOutboxStore, SqlOutboxStore>();

            return services;
        }

        public IServiceCollection AddOutboxDispatcher(IConfiguration configuration)
        {
            services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));

            services.AddSingleton<IOutboxPublisher, InProcessOutboxPublisher>();

            services.AddHostedService<OutboxDispatcher>();

            return services;
        }

        public IServiceCollection AddOutboxHandler<THandler>()
            where THandler : class, IOutboxMessageHandler
        {
            services.AddScoped<IOutboxMessageHandler, THandler>();

            return services;
        }

        public IServiceCollection AddNotifications(IConfiguration configuration)
        {
            services.AddOptions<SmtpOptions>()
                .Bind(configuration.GetSection(SmtpOptions.SectionName))
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.Host),
                    $"'{SmtpOptions.SectionName}:Host' is not configured.")
                .Validate(
                    options => options.Port is > 0 and <= 65535,
                    $"'{SmtpOptions.SectionName}:Port' is not a valid port.")
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.FromAddress),
                    $"'{SmtpOptions.SectionName}:FromAddress' is not configured.")
                .ValidateOnStart();

            services.AddSingleton<IEmailSender, SmtpEmailSender>();

            return services.AddOutboxHandler<SendBookingConfirmationHandler>();
        }

        private IServiceCollection AddPayments(IConfiguration configuration)
        {
            services.Configure<FakePaymentOptions>(configuration.GetSection(FakePaymentOptions.SectionName));

            services.AddSingleton<IPaymentGateway, FakePaymentGateway>();

            return services;
        }

        private IServiceCollection AddHealthDiagnostics()
        {
            services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>(HealthCheckNames.Database, tags: [HealthCheckNames.ReadyTag])
                .AddCheck<CacheHealthCheck>(HealthCheckNames.Cache, tags: [HealthCheckNames.ReadyTag]);

            return services;
        }

        private IServiceCollection AddCache(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString(RedisConnection.ConnectionName)
                                   ?? throw new InvalidOperationException(
                                       $"Connection string '{RedisConnection.ConnectionName}' is not configured.");

            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(RedisConnection.Configure(connectionString)));

            services.AddSingleton<ITokenDenylist, RedisTokenDenylist>();
            services.AddSingleton<ICacheService, RedisCacheService>();
            services.AddSingleton<ICartRepository, RedisCartRepository>();
            services.AddSingleton<RedisVisitStore>();
            services.AddSingleton<IVisitTracker>(provider => provider.GetRequiredService<RedisVisitStore>());
            services.AddSingleton<IVisitRankings>(provider => provider.GetRequiredService<RedisVisitStore>());

            return services;
        }

        private IServiceCollection AddAuthenticationServices(IConfiguration configuration)
        {
            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

            services.AddSingleton(_ =>
                configuration.GetSection(AuthenticationOptions.SectionName).Get<AuthenticationOptions>()
                ?? new AuthenticationOptions());

            services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
            services.AddSingleton<IRefreshTokenFactory, RefreshTokenFactory>();
            services.AddSingleton<IAccessTokenProvider, JwtAccessTokenProvider>();

            return services;
        }
    }
}
