using Microsoft.Extensions.Configuration;

namespace HotelBooking.Api.IntegrationTests.Infrastructure;

internal sealed class TestContainerImages
{
    private const string SectionName = "TestContainers";

    public string SqlServerImage { get; init; } = "mcr.microsoft.com/mssql/server:2025-CU9-ubuntu-24.04";

    public string RedisImage { get; init; } = "redis:8.2-alpine";

    public static TestContainerImages Load() =>
        new ConfigurationBuilder()
            .AddJsonFile("testsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build()
            .GetSection(SectionName)
            .Get<TestContainerImages>()
        ?? new TestContainerImages();
}
