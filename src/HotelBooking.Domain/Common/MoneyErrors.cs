using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

public static class MoneyErrors
{
    private const string Field = "amount";

    private const string CurrencyField = "currency";

    public static Error Negative => Error.Validation(
        "Money.Negative", Field, "An amount cannot be negative.");

    public static Error TooManyDecimalPlaces => Error.Validation(
        "Money.TooManyDecimalPlaces", Field, $"An amount may have at most {Money.DecimalPlaces} decimal places.");

    public static Error CurrencyRequired => Error.Validation(
        "Money.CurrencyRequired", CurrencyField, "Currency is required.");

    public static Error CurrencyInvalid => Error.Validation(
        "Money.CurrencyInvalid", CurrencyField, $"Currency must be {Money.CurrencyLength} letters, for example USD.");

    public static Error CurrencyMismatch => Error.Failure(
        "Money.CurrencyMismatch", "Amounts in different currencies cannot be combined.");

    public static Error NegativeFactor => Error.Failure(
        "Money.NegativeFactor", "An amount cannot be multiplied by a negative number.");
}
