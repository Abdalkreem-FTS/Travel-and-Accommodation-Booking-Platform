using HotelBooking.Application.Users.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Users;

public interface IUserService
{
    Task<Result<UserDto>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);
}
