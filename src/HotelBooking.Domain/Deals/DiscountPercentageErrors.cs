using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Deals;

public static class DiscountPercentageErrors
{
    public static Error OutOfRange => Error.Validation(
        "DiscountPercentage.OutOfRange",
        "discountPercentage",
        $"A discount must be between {DiscountPercentage.Minimum}% and {DiscountPercentage.Maximum}%: "
        + "nothing off is not a deal, and a free room is not a discount.");
}
