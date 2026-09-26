using HotelBooking.Domain.Carts;
using HotelBooking.Domain.Common;

namespace HotelBooking.Domain.UnitTests.Carts;

public sealed class CartTests
{
    private static readonly DateOnly Today = new(2026, 9, 26);

    private static readonly Guid UserId = new("00000000-0000-0000-0001-000000000001");
    private static readonly Guid HotelId = new("00000000-0000-0000-0003-000000000001");
    private static readonly Guid RoomId = new("00000000-0000-0000-0004-000000000001");

    private static CartItem AStay(
        Guid? roomId = null, int startsIn = 1, int nights = 2, decimal rate = 100m, string currency = "USD", int adults = 2)
    {
        var stay = DateRange.Create(Today.AddDays(startsIn), Today.AddDays(startsIn + nights), Today).Value;

        return CartItem.Restore(
            roomId ?? RoomId, HotelId, stay, Occupancy.Create(adults, 0).Value, Money.Create(rate, currency).Value).Value;
    }

    [Fact]
    public void Add_TheSameRoomForTheSameNightsAgain_ReplacesTheStayInsteadOfHoldingItTwice()
    {
        var cart = Cart.For(UserId);
        cart.Add(AStay(adults: 1));

        var result = cart.Add(AStay(adults: 2));

        result.IsSuccess.ShouldBeTrue();
        cart.Items.ShouldHaveSingleItem().Guests.Adults.ShouldBe(2, "the newer party size wins");
    }

    [Fact]
    public void Add_WhenTheCartIsFull_RefusesANewStayButStillAcceptsAChangeToOneItHolds()
    {
        var cart = Cart.For(UserId);

        for (var i = 0; i < Cart.MaxItems; i++)
        {
            cart.Add(AStay(roomId: Guid.NewGuid()));
        }

        cart.Add(AStay(roomId: Guid.NewGuid())).TopError.ShouldBe(CartErrors.Full);
        cart.Add(cart.Items[0]).IsSuccess.ShouldBeTrue("replacing a held stay does not grow the cart");
        cart.Items.Count.ShouldBe(Cart.MaxItems);
    }

    [Fact]
    public void Add_AStayPricedInAnotherCurrency_IsRefusedSoTheTotalNeverMixesCurrencies()
    {
        var cart = Cart.For(UserId);
        cart.Add(AStay(rate: 100m));

        var result = cart.Add(AStay(roomId: Guid.NewGuid(), currency: "EUR"));

        result.TopError.ShouldBe(CartErrors.CurrencyMismatch);
        cart.Items.ShouldHaveSingleItem();
        cart.TotalAmount.ShouldBe(200m);
    }

    [Fact]
    public void Prune_DropsOnlyTheStaysWhoseCheckInHasPassed()
    {
        var stale = AStay(startsIn: 1);
        var fresh = AStay(roomId: Guid.NewGuid(), startsIn: 3);
        var cart = Cart.Restore(UserId, [stale, fresh]);

        var dropped = cart.Prune(Today.AddDays(2));

        dropped.ShouldBe([stale.Id]);
        cart.Items.ShouldHaveSingleItem().Id.ShouldBe(fresh.Id);
    }
}
