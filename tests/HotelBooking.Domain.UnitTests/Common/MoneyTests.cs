using HotelBooking.Domain.Common;

namespace HotelBooking.Domain.UnitTests.Common;

public sealed class MoneyTests
{
    private static Money Usd(decimal amount) => Money.Create(amount, "USD").Value;

    [Fact]
    public void Create_WithAValidAmount_NormalisesTheCurrencySoTwoPricesInUsdCompareEqual()
    {
        var result = Money.Create(120.50m, " usd ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(120.50m);
        result.Value.Currency.ShouldBe("USD");
        result.Value.ShouldBe(Usd(120.50m));
    }

    [Fact]
    public void Create_WithMoreDecimalPlacesThanTheColumnHolds_IsRejectedRatherThanSilentlyRounded()
    {
        Money.Create(10.005m, "USD").Errors.ShouldContain(MoneyErrors.TooManyDecimalPlaces);
    }

    [Fact]
    public void Add_ForTwoAmounts_SumsTheSameCurrencyAndRefusesADifferentOne()
    {
        Usd(120.50m).Add(Usd(9.50m)).Value.ShouldBe(Usd(130m));

        Usd(120m).Add(Money.Create(120m, "EUR").Value).TopError.ShouldBe(MoneyErrors.CurrencyMismatch);
    }

    [Fact]
    public void Multiply_ByANightCount_ScalesTheAmountAndRefusesANegativeFactor()
    {
        var nights = Usd(120.50m).Multiply(3);

        nights.Value.Amount.ShouldBe(361.50m);
        nights.Value.Currency.ShouldBe("USD");

        Usd(120m).Multiply(-1).TopError.ShouldBe(MoneyErrors.NegativeFactor);
    }

    [Fact]
    public void Subtract_ForTwoAmounts_TakesOneOffTheOtherAndRefusesToGoBelowZero()
    {
        Usd(300m).Subtract(Usd(30m)).Value.ShouldBe(Usd(270m));

        Usd(300m).Subtract(Usd(300m)).Value.ShouldBe(
            Usd(0m), "taking everything off leaves nothing, which is still an amount");

        Usd(30m).Subtract(Usd(300m)).TopError.ShouldBe(
            MoneyErrors.Negative, "money has no negative side to wrap round into");

        Usd(300m).Subtract(Money.Create(30m, "EUR").Value).TopError
            .ShouldBe(MoneyErrors.CurrencyMismatch);
    }
}
