using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Payments;

public interface IPaymentGateway
{
    Task<Result<PaymentAuthorization>> AuthorizeAsync(
        Guid userId,
        Money amount,
        string reference,
        CancellationToken cancellationToken = default);

    Task<Result<Success>> CaptureAsync(
        PaymentAuthorization authorization,
        CancellationToken cancellationToken = default);

    Task<Result<Success>> VoidAsync(
        PaymentAuthorization authorization,
        CancellationToken cancellationToken = default);
}
