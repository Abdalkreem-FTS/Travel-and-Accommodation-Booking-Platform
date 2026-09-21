using System.Globalization;

using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Deals;

public sealed record DiscountPercentage
{
    public const int Minimum = 1;

    public const int Maximum = 90;

    private const int PercentageBase = 100;

    private DiscountPercentage(int value) => Value = value;

    public int Value { get; }

    public static Result<DiscountPercentage> Create(int value) =>
        value is < Minimum or > Maximum
            ? DiscountPercentageErrors.OutOfRange
            : new DiscountPercentage(value);

    public Result<Money> ApplyTo(Money original)
    {
        var discounted = original.Amount * (PercentageBase - Value) / PercentageBase;

        return Money.Create(
            decimal.Round(discounted, Money.DecimalPlaces, MidpointRounding.AwayFromZero),
            original.Currency);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture) + "%";
}
