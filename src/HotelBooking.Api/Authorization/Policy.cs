namespace HotelBooking.Api.Authorization;

public static class Policy
{
    public const string AuthenticatedUser = nameof(AuthenticatedUser);

    public const string AdminOnly = nameof(AdminOnly);
}
