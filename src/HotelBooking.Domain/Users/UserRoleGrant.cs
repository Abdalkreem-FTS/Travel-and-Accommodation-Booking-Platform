namespace HotelBooking.Domain.Users;

public sealed record UserRoleGrant
{
    private UserRoleGrant(UserRole role, DateTimeOffset grantedAtUtc)
    {
        Role = role;
        GrantedAtUtc = grantedAtUtc;
    }

    public UserRole Role { get; private set; }

    public DateTimeOffset GrantedAtUtc { get; private set; }

    internal static UserRoleGrant Of(UserRole role, DateTimeOffset grantedAtUtc) => new(role, grantedAtUtc);
}
