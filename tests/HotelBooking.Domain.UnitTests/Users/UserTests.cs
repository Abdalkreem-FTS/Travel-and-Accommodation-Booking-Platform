using HotelBooking.Domain.Common;
using HotelBooking.Domain.Users;
using HotelBooking.Domain.Users.Events;

namespace HotelBooking.Domain.UnitTests.Users;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_RaisesUserRegisteredCarryingWhatTheWelcomeEmailNeeds()
    {
        var userId = new Guid("00000000-0000-0000-0001-000000000001");
        var eventId = new Guid("00000000-0000-0000-0006-000000000001");

        var user = User.Register(
            userId, eventId, Email.Create("abdalkreem@example.com").Value, "hash", "Abdalkreem", "Bzoor", Now).Value;

        var registered = user.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<UserRegistered>();
        registered.EventId.ShouldBe(eventId);
        registered.UserId.ShouldBe(userId);
        registered.Email.ShouldBe("abdalkreem@example.com");
        registered.FirstName.ShouldBe("Abdalkreem");
    }
}
