using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Carts;

public static class CartErrors
{
    public static Error UserRequired => Error.Validation(
        "Cart.UserRequired", "userId", "A cart must belong to a user.");

    public static Error RoomCannotHostParty => Error.Validation(
        "Cart.RoomCannotHostParty", "adults", "That room cannot host a party of that size.");

    public static Error Full => Error.Conflict(
        "Cart.Full", $"A cart holds at most {Cart.MaxItems} stays. Remove one before adding another.");

    public static Error CurrencyMismatch => Error.Conflict(
        "Cart.CurrencyMismatch",
        "Every stay in a cart must be priced in the same currency. Check out the stays you have "
        + "before adding one priced differently.");

    public static Error ItemNotFound => Error.NotFound(
        "Cart.ItemNotFound", "That stay is not in your cart.");

    public static Error Empty => Error.Validation(
        "Cart.Empty",
        "items",
        "Your cart is empty. Add a stay to it, or send the stays to book in the request body.");

    public static Error Unavailable => Error.Unavailable(
        "Cart.Unavailable", "Carts are temporarily unavailable. Please try again in a moment.");
}
