using HotelBooking.Api.Endpoints;
using HotelBooking.Api.IntegrationTests.Observability;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Users;
using HotelBooking.Infrastructure.Persistence;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry.Trace;

using Respawn;

using StackExchange.Redis;

using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace HotelBooking.Api.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SigningKey = "integration-test-signing-key-that-is-long-enough-for-hs256";

    private static readonly TestContainerImages Images = TestContainerImages.Load();

    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder(Images.SqlServerImage).Build();

    private readonly RedisContainer _redis = new RedisBuilder(Images.RedisImage).Build();

    private ConnectionMultiplexer? _redisClient;

    private Respawner? _respawner;
    private SqlConnection? _resetConnection;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_sqlServer.StartAsync(), _redis.StartAsync());

        var redisOptions = ConfigurationOptions.Parse(_redis.GetConnectionString());
        redisOptions.AllowAdmin = true;

        _redisClient = await ConnectionMultiplexer.ConnectAsync(redisOptions);

        StartServer();

        _resetConnection = new SqlConnection(_sqlServer.GetConnectionString());
        await _resetConnection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_resetConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = ["__EFMigrationsHistory", "Amenities"]
        });
    }

    public Task ResetDatabaseAsync() => Respawner.ResetAsync(ResetConnection);

    public Task ResetCacheAsync() =>
        RedisClient.GetServer(RedisClient.GetEndPoints()[0]).FlushDatabaseAsync();

    public IDatabase Cache => RedisClient.GetDatabase();

    public SpanCollection Spans { get; } = [];

    private Respawner Respawner => _respawner ?? throw NotInitialised();

    private SqlConnection ResetConnection => _resetConnection ?? throw NotInitialised();

    private ConnectionMultiplexer RedisClient => _redisClient ?? throw NotInitialised();

    private static InvalidOperationException NotInitialised() =>
        new("The API fixture is used before InitializeAsync has run.");

    public async Task PromoteToAdminAsync(string email)
    {
        using var scope = Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();

        var user = await scope.ServiceProvider
            .GetRequiredService<IUserRepository>()
            .GetByEmailAsync(Email.Create(email).Value)
                   ?? throw new InvalidOperationException($"No user is registered as '{email}'.");

        context.Entry(user).Property(u => u.Role).CurrentValue = UserRole.Admin;

        await context.SaveChangesAsync();
    }

    public async Task<Guid> UserIdAsync(string email)
    {
        using var scope = Services.CreateScope();

        var user = await scope.ServiceProvider
            .GetRequiredService<IUserRepository>()
            .GetByEmailAsync(Email.Create(email).Value)
                   ?? throw new InvalidOperationException($"No user is registered as '{email}'.");

        return user.Id;
    }

    public HttpClient CreateClientWithUnreachableCache() =>
        WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Redis", "127.0.0.1:1,connectTimeout=100,abortConnect=false"))
            .CreateClient();

    private HttpClient CreateClientWithSettings(params (string Key, string Value)[] settings) =>
        WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        }).CreateClient();

    public HttpClient CreateClientWithSetting(string key, string value) =>
        CreateClientWithSettings((key, value));

    public override async ValueTask DisposeAsync()
    {
        if (_resetConnection is not null)
        {
            await _resetConnection.DisposeAsync();
        }

        if (_redisClient is not null)
        {
            await _redisClient.DisposeAsync();
        }

        await base.DisposeAsync();
        await _sqlServer.DisposeAsync();
        await _redis.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.UseSetting("ConnectionStrings:Database", _sqlServer.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("Authentication:Jwt:SigningKey", SigningKey);

        builder.UseSetting("RateLimits:Enabled", "false");

        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);

        builder.ConfigureServices(services =>
        {
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(Spans));

            services.AddSingleton<IEndpoint, ProtectedProbeEndpoint>();
            services.AddSingleton<IEndpoint, AdminProbeEndpoint>();
        });
    }
}
