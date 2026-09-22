using System.Data;
using System.Data.Common;

using HotelBooking.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelBooking.Infrastructure.Outbox;

internal sealed class SqlOutboxStore(HotelBookingDbContext context) : IOutboxStore
{
    private const int MaxErrorLength = 2000;

    private const string ClaimSql =
        """
        WITH candidates AS (
            SELECT TOP (@batchSize) Id, Type, Content, Attempts, ProcessingAtUtc, TraceParent
            FROM OutboxMessages WITH (READPAST, UPDLOCK, ROWLOCK)
            WHERE ProcessedOnUtc IS NULL
              AND Attempts < @maxAttempts
              AND (ProcessingAtUtc IS NULL
                   OR ProcessingAtUtc < DATEADD(second, -@leaseSeconds, SYSUTCDATETIME()))
            ORDER BY OccurredOnUtc)
        UPDATE candidates
        SET ProcessingAtUtc = SYSUTCDATETIME(),
            Attempts = Attempts + 1
        OUTPUT inserted.Id, inserted.Type, inserted.Content, inserted.Attempts, inserted.TraceParent;
        """;

    public async Task<IReadOnlyList<OutboxClaim>> ClaimAsync(
        int batchSize,
        TimeSpan lease,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            async token =>
            {
                await context.Database.OpenConnectionAsync(token);

                try
                {
                    await using var command = context.Database.GetDbConnection().CreateCommand();

                    command.CommandText = ClaimSql;
                    command.CommandTimeout = context.Database.GetCommandTimeout() ?? command.CommandTimeout;
                    command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();

                    Bind(command, "@batchSize", DbType.Int32, batchSize);
                    Bind(command, "@maxAttempts", DbType.Int32, maxAttempts);
                    Bind(command, "@leaseSeconds", DbType.Int32, (int)Math.Ceiling(lease.TotalSeconds));

                    await using var reader = await command.ExecuteReaderAsync(token);

                    var claimed = new List<OutboxClaim>();

                    while (await reader.ReadAsync(token))
                    {
                        claimed.Add(new OutboxClaim(
                            reader.GetGuid(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetInt32(3),
                            reader.IsDBNull(4) ? null : reader.GetString(4)));
                    }

                    return (IReadOnlyList<OutboxClaim>)claimed;
                }
                finally
                {
                    await context.Database.CloseConnectionAsync();
                }
            },
            cancellationToken);
    }

    public async Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Database.ExecuteSqlAsync(
            $"""
             UPDATE OutboxMessages
             SET ProcessedOnUtc = SYSUTCDATETIME(), ProcessingAtUtc = NULL, Error = NULL
             WHERE Id = {id}
             """,
            cancellationToken);

    public async Task MarkFailedAsync(Guid id, string failure, CancellationToken cancellationToken = default)
    {
        var recorded = failure.Length > MaxErrorLength ? failure[..MaxErrorLength] : failure;

        await context.Database.ExecuteSqlAsync(
            $"UPDATE OutboxMessages SET Error = {recorded} WHERE Id = {id}",
            cancellationToken);
    }

    public async Task<int> CountPendingAsync(int maxAttempts, CancellationToken cancellationToken = default) =>
        await context.Database
            .SqlQuery<int>(
                $"""
                 SELECT COUNT(*) AS Value
                 FROM OutboxMessages
                 WHERE ProcessedOnUtc IS NULL AND Attempts < {maxAttempts}
                 """)
            .SingleAsync(cancellationToken);

    private static void Bind(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();

        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;

        command.Parameters.Add(parameter);
    }
}
