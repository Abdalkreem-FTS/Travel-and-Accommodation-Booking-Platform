using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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

    [Fact]
    public async Task Get_WithTheGatewaysTraceparent_AnswersUnderThatTrace()
    {
        const string gatewayTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/hotels/{Guid.NewGuid()}");

        request.Headers.Add("traceparent", $"00-{gatewayTraceId}-00f067aa0ba902b7-01");

        using var response = await Client.SendAsync(request, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Token);

        response.Headers.GetValues("X-Request-Id").Single().ShouldBe(gatewayTraceId);
        problem.GetProperty("traceId").GetString().ShouldBe(gatewayTraceId);
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
