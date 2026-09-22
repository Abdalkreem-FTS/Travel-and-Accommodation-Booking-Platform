namespace HotelBooking.Application.Users.Dtos;

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName);
