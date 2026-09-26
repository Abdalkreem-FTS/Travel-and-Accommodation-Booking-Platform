using HotelBooking.Domain.Common;
using HotelBooking.Domain.Deals;

namespace HotelBooking.Domain.UnitTests.Deals;

public sealed class DiscountPercentageTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public void Create_OutsideOneToNinety_IsRefused(int value)
    {
        DiscountPercentage.Create(value).TopError.ShouldBe(DiscountPercentageErrors.OutOfRange);
    }

    [Fact]
    public void ApplyTo_RoundsTheHalfCentAwayFromZero()
    {
        var price = Money.Create(99.99m, "USD").Value;

        var discounted = DiscountPercentage.Create(15).Value.ApplyTo(price).Value;

        discounted.ShouldBe(Money.Create(84.99m, "USD").Value, "99.99 * 0.85 = 84.9915");
        DiscountPercentage.Create(50).Value.ApplyTo(Money.Create(0.05m, "USD").Value).Value.Amount
            .ShouldBe(0.03m, "0.025 rounds up, not to the even 0.02");
    }
}
