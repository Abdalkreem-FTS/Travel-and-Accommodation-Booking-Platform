using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

public static class StarRatingErrors
{
    public static Error OutOfRange => Error.Validation(
        "StarRating.OutOfRange",
        "starRating",
        $"Star rating must be between {StarRating.Minimum} and {StarRating.Maximum}.");
}
