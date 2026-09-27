using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Abstractions;

public interface IUnitOfWork
{
    Task<Result<Success>> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<Result<TValue>> ExecuteInTransactionAsync<TValue>(
        Func<CancellationToken, Task<Result<TValue>>> operation,
        CancellationToken cancellationToken = default);
}
