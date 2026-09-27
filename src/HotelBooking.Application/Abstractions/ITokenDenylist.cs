namespace HotelBooking.Application.Abstractions;

public interface ITokenDenylist
{
    Task DenySessionAsync(
        Guid sessionId,
        TimeSpan revocationWindow,
        CancellationToken cancellationToken = default);

    Task<bool> IsSessionDeniedAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
