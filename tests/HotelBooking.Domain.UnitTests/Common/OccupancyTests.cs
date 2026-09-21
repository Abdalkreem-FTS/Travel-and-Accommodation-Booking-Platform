using HotelBooking.Domain.Common;

namespace HotelBooking.Domain.UnitTests.Common;

public sealed class OccupancyTests
{
    private static Occupancy For(int adults, int children) => Occupancy.Create(adults, children).Value;

    [Fact]
    public void Create_AtTheGuestCap_IsAllowedButOneOverIsNot()
    {
        Occupancy.Create(Occupancy.MaximumGuests - 1, 1).IsSuccess.ShouldBeTrue();

        Occupancy.Create(Occupancy.MaximumGuests, 1).Errors.ShouldContain(OccupancyErrors.TooManyGuests);
    }

    [Fact]
    public void Accommodates_ForAParty_ComparesAdultsAndChildrenSeparatelyNotJustTheTotal()
    {
        For(2, 1).Accommodates(For(2, 1)).ShouldBeTrue();
        For(4, 2).Accommodates(For(1, 0)).ShouldBeTrue();
        For(2, 2).Accommodates(For(3, 0)).ShouldBeFalse();
        For(4, 0).Accommodates(For(1, 1)).ShouldBeFalse();
    }
}
