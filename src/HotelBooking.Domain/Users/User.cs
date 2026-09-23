using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Users.Events;

namespace HotelBooking.Domain.Users;

public sealed class User : AggregateRoot<Guid>
{
    public const int MaxNameLength = 100;

    private readonly List<UserRoleGrant> _roles = [];

    private User(
        Guid id,
        Email email,
        string passwordHash,
        string firstName,
        string lastName,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        CreatedAtUtc = createdAtUtc;
    }

    private User()
    {
        Email = null!;
        PasswordHash = null!;
        FirstName = null!;
        LastName = null!;
    }

    public Email Email { get; private set; }

    public string PasswordHash { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public IReadOnlyList<UserRoleGrant> Roles => _roles;

    public static Result<User> Register(
        Guid id,
        Guid eventId,
        Email email,
        string passwordHash,
        string? firstName,
        string? lastName,
        DateTimeOffset nowUtc)
    {
        List<Error> errors = [];

        var first = Validate(firstName, UserErrors.FirstNameRequired, UserErrors.FirstNameTooLong, errors);
        var last = Validate(lastName, UserErrors.LastNameRequired, UserErrors.LastNameTooLong, errors);

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            errors.Add(UserErrors.PasswordHashRequired);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var user = new User(id, email, passwordHash, first, last, nowUtc);

        user._roles.Add(UserRoleGrant.Of(UserRole.User, nowUtc));

        // user.Raise(new UserRegistered(eventId, user.Id, email.Value, user.FirstName, nowUtc));
        _ = eventId;

        return user;
    }

    public bool HasRole(UserRole role) => _roles.Exists(grant => grant.Role == role);

    public Result<Updated> Grant(UserRole role, DateTimeOffset nowUtc)
    {
        if (HasRole(role))
        {
            return Result.Updated;
        }

        _roles.Add(UserRoleGrant.Of(role, nowUtc));

        return Result.Updated;
    }

    public Result<Updated> Revoke(UserRole role)
    {
        var grant = _roles.Find(held => held.Role == role);

        if (grant is null)
        {
            return UserErrors.RoleNotGranted;
        }

        if (_roles.Count == 1)
        {
            return UserErrors.LastRoleCannotBeRevoked;
        }

        _roles.Remove(grant);

        return Result.Updated;
    }

    private static string Validate(string? value, Error required, Error tooLong, List<Error> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(required);
            return string.Empty;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            errors.Add(tooLong);
        }

        return trimmed;
    }
}
