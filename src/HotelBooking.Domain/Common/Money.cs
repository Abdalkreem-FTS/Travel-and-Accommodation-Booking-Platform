using System.Globalization;

using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

public sealed record Money
{
    public const int CurrencyLength = 3;

    public const int DecimalPlaces = 2;

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public bool IsPositive => Amount > 0m;

    public static Result<Money> Create(decimal amount, string? currency)
    {
        List<Error> errors = [];

        if (amount < 0m)
        {
            errors.Add(MoneyErrors.Negative);
        }
        else if (decimal.Round(amount, DecimalPlaces) != amount)
        {
            errors.Add(MoneyErrors.TooManyDecimalPlaces);
        }

        var normalised = currency?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(normalised))
        {
            errors.Add(MoneyErrors.CurrencyRequired);
        }
        else if (normalised.Length != CurrencyLength || !normalised.All(char.IsAsciiLetterUpper))
        {
            errors.Add(MoneyErrors.CurrencyInvalid);
        }

        return errors.Count > 0 ? errors : new Money(amount, normalised!);
    }

    public Result<Money> Add(Money other) =>
        string.Equals(other.Currency, Currency, StringComparison.Ordinal)
            ? new Money(Amount + other.Amount, Currency)
            : MoneyErrors.CurrencyMismatch;

    public Result<Money> Subtract(Money other)
    {
        if (!string.Equals(other.Currency, Currency, StringComparison.Ordinal))
        {
            return MoneyErrors.CurrencyMismatch;
        }

        return Amount < other.Amount
            ? MoneyErrors.Negative
            : new Money(Amount - other.Amount, Currency);
    }

    public Result<Money> Multiply(int factor) =>
        factor < 0
            ? MoneyErrors.NegativeFactor
            : new Money(Amount * factor, Currency);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");
}
