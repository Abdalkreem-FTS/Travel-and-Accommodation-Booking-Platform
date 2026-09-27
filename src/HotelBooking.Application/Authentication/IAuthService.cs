using HotelBooking.Application.Authentication.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Authentication;

public interface IAuthService
{
    Task<Result<SessionDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<Result<SessionDto>> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken = default);

    Task<Result<Deleted>> LogoutAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
