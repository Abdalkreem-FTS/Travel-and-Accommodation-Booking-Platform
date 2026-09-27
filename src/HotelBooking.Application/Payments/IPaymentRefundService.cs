using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Payments;

public interface IPaymentRefundService
{
    Task<Result<Success>> RefundAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<int> SendPendingAsync(int batchSize, CancellationToken cancellationToken = default);
}
