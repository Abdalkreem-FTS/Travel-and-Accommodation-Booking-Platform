using System.Diagnostics;

using HotelBooking.Api.IntegrationTests.Infrastructure;

namespace HotelBooking.Api.IntegrationTests.Observability;

public sealed class RequestTraceTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ForASearch_IsOneTraceReachingBothSqlAndRedis()
    {
        using var response = await Client.GetAsync("/api/hotels?page=1&pageSize=10", Token);

        response.EnsureSuccessStatusCode();

        var spans = await Factory.Spans.WaitForAsync(
            IsComplete, "a search produced no SQL span, no Redis span, or neither", Token);

        var search = Search(spans).ShouldNotBeNull();

        var trace = spans.Where(span => span.TraceId == search.TraceId).ToList();

        var sql = trace.Where(span => span.Source.Name == Traces.SqlSource).ToList();
        var redis = trace.Where(span => span.Source.Name == Traces.RedisSource).ToList();

        sql.ShouldNotBeEmpty("the search reads the catalogue out of SQL Server");
        redis.ShouldNotBeEmpty("the search asks the cache before it asks the database");

        sql.ShouldAllBe(span => span.Kind == ActivityKind.Client);
        redis.ShouldAllBe(span => span.Kind == ActivityKind.Client);
    }

    private static bool IsComplete(IReadOnlyList<Activity> recorded)
    {
        if (Search(recorded) is not { } search)
        {
            return false;
        }

        var trace = recorded.Where(span => span.TraceId == search.TraceId).ToList();

        return trace.Any(span => span.Source.Name == Traces.SqlSource)
               && trace.Any(span => span.Source.Name == Traces.RedisSource);
    }

    private static Activity? Search(IReadOnlyList<Activity> recorded) =>
        recorded.FirstOrDefault(span =>
            span.Kind == ActivityKind.Server
            && span.DisplayName.Contains("hotels", StringComparison.OrdinalIgnoreCase));
}
