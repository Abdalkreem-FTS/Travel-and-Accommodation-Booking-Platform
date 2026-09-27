using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Payments;

public interface IPaymentService
{
    Task<Result<Success>> HandleEventAsync(
        string payload,
        string? signature,
        CancellationToken cancellationToken = default);

    Task<int> ExpireOverdueAsync(int batchSize, CancellationToken cancellationToken = default);
}
