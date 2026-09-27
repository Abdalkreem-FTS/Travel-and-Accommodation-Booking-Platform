using HotelBooking.Domain.Payments;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Repositories;

internal sealed class PaymentRepository(HotelBookingDbContext context) : IPaymentRepository
{
    public void Add(Payment payment) => context.Payments.Add(payment);

    public Task<Payment?> GetAsync(Guid paymentId, CancellationToken cancellationToken = default) =>
        context.Payments
            .FirstOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListOverdueAsync(
        DateTimeOffset nowUtc,
        int limit,
        CancellationToken cancellationToken = default) =>
        await context.Payments
            .AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Pending && payment.ExpiresAtUtc <= nowUtc)
            .OrderBy(payment => payment.ExpiresAtUtc)
            .ThenBy(payment => payment.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListRefundingAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        await context.Payments
            .AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Refunding)
            .OrderBy(payment => payment.RefundRequestedAtUtc)
            .ThenBy(payment => payment.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<Payment?> GetForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default) =>
        context.Payments
            .FirstOrDefaultAsync(payment => payment.BookingId == bookingId, cancellationToken);

    public Task<Payment?> GetByCheckoutIdAsync(
        string providerCheckoutId,
        CancellationToken cancellationToken = default) =>
        context.Payments
            .FirstOrDefaultAsync(payment => payment.ProviderCheckoutId == providerCheckoutId, cancellationToken);
}
