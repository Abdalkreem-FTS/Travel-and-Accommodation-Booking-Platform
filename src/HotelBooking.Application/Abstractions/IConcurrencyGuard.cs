using HotelBooking.Application.Common;

namespace HotelBooking.Application.Abstractions;

public interface IConcurrencyGuard
{
    ConcurrencyToken TokenFor<TEntity>(TEntity entity)
        where TEntity : class;

    void Expect<TEntity>(TEntity entity, ConcurrencyToken expected)
        where TEntity : class;
}
