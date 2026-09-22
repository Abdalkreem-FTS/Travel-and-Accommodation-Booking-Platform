using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Hotels;

public static class HotelSearchErrors
{
    public static Error PriceNegative => Error.Validation(
        "Hotel.PriceNegative", "minPrice", "A price filter cannot be negative.");

    public static Error PriceRangeInverted => Error.Validation(
        "Hotel.PriceRangeInverted", "maxPrice", "maxPrice must be at least minPrice.");

    public static Error SortUnknown => Error.Validation(
        "Hotel.SortUnknown", "sort", "Sort by price, stars or name.");

    public static Error RoomTypeUnknown => Error.Validation(
        "Hotel.RoomTypeUnknown", "roomType", "No room type goes by that name.");
}
