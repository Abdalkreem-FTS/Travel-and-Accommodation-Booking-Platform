using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Users;

public interface IUserRoleService
{
    Task<Result<Updated>> GrantAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);

    Task<Result<Deleted>> RevokeAsync(
        Guid userId,
        string role,
        Guid actingUserId,
        CancellationToken cancellationToken = default);
}
