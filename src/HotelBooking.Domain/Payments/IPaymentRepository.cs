namespace HotelBooking.Domain.Payments;

public interface IPaymentRepository
{
    void Add(Payment payment);

    Task<Payment?> GetAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Payment>> ListOverdueAsync(
        DateTimeOffset nowUtc,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Payment>> ListRefundingAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<Payment?> GetForBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<Payment?> GetByCheckoutIdAsync(string providerCheckoutId, CancellationToken cancellationToken = default);
}
