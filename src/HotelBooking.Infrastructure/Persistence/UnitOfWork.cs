using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Results;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence;

public sealed class UnitOfWork(HotelBookingDbContext context) : IUnitOfWork
{
    public async Task<Result<Success>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return Result.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();

            return PersistenceErrors.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (SqlErrorTranslator.Translate(exception) is { } error)
        {
            context.ChangeTracker.Clear();

            return error;
        }
    }

    public async Task<Result<TValue>> ExecuteInTransactionAsync<TValue>(
        Func<CancellationToken, Task<Result<TValue>>> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        var attempt = 0;

        return await strategy.ExecuteAsync(
            async token =>
            {
                if (attempt++ > 0)
                {
                    context.ChangeTracker.Clear();
                }

                await using var transaction = await context.Database.BeginTransactionAsync(token);

                var result = await operation(token);

                if (result.IsSuccess)
                {
                    await transaction.CommitAsync(token);
                }
                else
                {
                    await transaction.RollbackAsync(token);
                }

                return result;
            },
            cancellationToken);
    }
}
