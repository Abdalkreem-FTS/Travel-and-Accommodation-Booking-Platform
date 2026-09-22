using System.Collections.Concurrent;

using HotelBooking.Application.Payments;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelBooking.Infrastructure.Payments;

internal sealed class FakePaymentGateway(
    IOptions<FakePaymentOptions> options,
    ILogger<FakePaymentGateway> logger) : IPaymentGateway
{
    private enum HoldState
    {
        Authorized,
        Captured,
        Voided
    }

    private readonly ConcurrentDictionary<string, HoldState> _holds = new(StringComparer.Ordinal);

    public Task<Result<PaymentAuthorization>> AuthorizeAsync(
        Guid userId,
        Money amount,
        string reference,
        CancellationToken cancellationToken = default)
    {
        if (options.Value.DeclineAuthorizations)
        {
            logger.LogInformation(
                "Declined an authorization of {Amount} {Currency} for user {UserId}",
                amount.Amount, amount.Currency, userId);

            return Task.FromResult<Result<PaymentAuthorization>>(PaymentErrors.Declined);
        }

        var id = $"auth_{reference}";

        _holds[id] = HoldState.Authorized;

        logger.LogInformation(
            "Authorized {Amount} {Currency} for user {UserId} against reference {Reference}",
            amount.Amount, amount.Currency, userId, reference);

        return Task.FromResult<Result<PaymentAuthorization>>(new PaymentAuthorization(id, amount));
    }

    public Task<Result<Success>> CaptureAsync(
        PaymentAuthorization authorization,
        CancellationToken cancellationToken = default)
    {
        if (options.Value.FailCaptures)
        {
            logger.LogWarning("Capture of authorization {AuthorizationId} failed", authorization.Id);

            return Task.FromResult<Result<Success>>(PaymentErrors.CaptureFailed);
        }

        if (!_holds.TryGetValue(authorization.Id, out var state) || state is HoldState.Voided)
        {
            logger.LogWarning(
                "Capture of authorization {AuthorizationId} failed: it is {State}",
                authorization.Id, state);

            return Task.FromResult<Result<Success>>(PaymentErrors.CaptureFailed);
        }

        _holds[authorization.Id] = HoldState.Captured;

        logger.LogInformation(
            "Captured {Amount} {Currency} against authorization {AuthorizationId}",
            authorization.Amount.Amount, authorization.Amount.Currency, authorization.Id);

        return Task.FromResult<Result<Success>>(Result.Success);
    }

    public Task<Result<Success>> VoidAsync(
        PaymentAuthorization authorization,
        CancellationToken cancellationToken = default)
    {
        _holds[authorization.Id] = HoldState.Voided;

        logger.LogInformation("Voided authorization {AuthorizationId}", authorization.Id);

        return Task.FromResult<Result<Success>>(Result.Success);
    }
}
