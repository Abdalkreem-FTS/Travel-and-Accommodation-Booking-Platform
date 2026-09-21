using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

public static class GeoLocationErrors
{
    private const string LatitudeField = "latitude";

    private const string LongitudeField = "longitude";

    public static Error LatitudeOutOfRange => Error.Validation(
        "GeoLocation.LatitudeOutOfRange", LatitudeField, "Latitude must be between -90 and 90.");

    public static Error LongitudeOutOfRange => Error.Validation(
        "GeoLocation.LongitudeOutOfRange", LongitudeField, "Longitude must be between -180 and 180.");

    public static Error LatitudeTooPrecise => Error.Validation(
        "GeoLocation.LatitudeTooPrecise",
        LatitudeField,
        $"Latitude may have at most {GeoLocation.DecimalPlaces} decimal places.");

    public static Error LongitudeTooPrecise => Error.Validation(
        "GeoLocation.LongitudeTooPrecise",
        LongitudeField,
        $"Longitude may have at most {GeoLocation.DecimalPlaces} decimal places.");
}
