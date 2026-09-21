using HotelBooking.Domain.Common;

namespace HotelBooking.Domain.Users;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken = default);

    void Add(User user);
}
