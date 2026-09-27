using System.Diagnostics;

namespace HotelBooking.Api.IntegrationTests.Observability;

internal static class Traces
{
    public const string SqlSource = "OpenTelemetry.Instrumentation.SqlClient";

    public const string RedisSource = "OpenTelemetry.Instrumentation.StackExchangeRedis";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static async Task<IReadOnlyList<Activity>> WaitForAsync(
        this SpanCollection spans,
        Func<IReadOnlyList<Activity>, bool> until,
        string because,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + Timeout;

        while (true)
        {
            var recorded = spans.Snapshot();

            if (until(recorded))
            {
                return recorded;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"{because}. Exported: {Describe(recorded)}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    public static string? Text(this Activity span) =>
        span.GetTagItem("db.query.text") as string ?? span.GetTagItem("db.statement") as string;

    private static string Describe(IReadOnlyList<Activity> spans) =>
        spans.Count == 0
            ? "nothing"
            : string.Join(
                ", ",
                spans.Select(span => $"{span.DisplayName} [{span.Kind}, trace {span.TraceId}]"));
}
