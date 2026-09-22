using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Common;

namespace HotelBooking.Infrastructure.Persistence;

internal sealed class EfConcurrencyGuard(HotelBookingDbContext context) : IConcurrencyGuard
{
    public ConcurrencyToken TokenFor<TEntity>(TEntity entity)
        where TEntity : class =>
        ConcurrencyToken.From(context.Entry(entity).Property<byte[]>(RowVersionProperty.Name).CurrentValue);

    public void Expect<TEntity>(TEntity entity, ConcurrencyToken expected)
        where TEntity : class =>
        context.Entry(entity).Property<byte[]>(RowVersionProperty.Name).OriginalValue = expected.Value;
}
