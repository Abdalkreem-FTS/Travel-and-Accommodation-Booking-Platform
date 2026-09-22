using HotelBooking.Domain.Users;

namespace HotelBooking.Api.Authorization;

public static class Roles
{
    public const string User = nameof(UserRole.User);

    public const string Admin = nameof(UserRole.Admin);
}
