using HotelBooking.Domain.Users;

namespace HotelBooking.Application.Users.Dtos;

public sealed record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role)
{
    public static UserDto From(User user) => new(
        user.Id,
        user.Email.Value,
        user.FirstName,
        user.LastName,
        user.Role.ToString());
}
