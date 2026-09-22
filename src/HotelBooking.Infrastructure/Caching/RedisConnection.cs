using StackExchange.Redis;

namespace HotelBooking.Infrastructure.Caching;

public static class RedisConnection
{
    public const string ConnectionName = "Redis";

    public static ConfigurationOptions Configure(string connectionString)
    {
        var options = ConfigurationOptions.Parse(connectionString);

        options.AbortOnConnectFail = false;
        options.ConnectTimeout = (int)TimeSpan.FromSeconds(2).TotalMilliseconds;

        options.SyncTimeout = (int)TimeSpan.FromMilliseconds(500).TotalMilliseconds;
        options.AsyncTimeout = (int)TimeSpan.FromMilliseconds(500).TotalMilliseconds;

        return options;
    }
}
