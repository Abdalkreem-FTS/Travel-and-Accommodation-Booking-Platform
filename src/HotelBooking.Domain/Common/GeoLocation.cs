using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

public sealed record GeoLocation
{
    public const int DecimalPlaces = 6;

    private const decimal LatitudeBound = 90m;

    private const decimal LongitudeBound = 180m;

    private GeoLocation(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public decimal Latitude { get; }

    public decimal Longitude { get; }

    public static Result<GeoLocation> Create(decimal latitude, decimal longitude)
    {
        List<Error> errors = [];

        Validate(
            latitude,
            LatitudeBound,
            GeoLocationErrors.LatitudeOutOfRange,
            GeoLocationErrors.LatitudeTooPrecise,
            errors);

        Validate(
            longitude,
            LongitudeBound,
            GeoLocationErrors.LongitudeOutOfRange,
            GeoLocationErrors.LongitudeTooPrecise,
            errors);

        return errors.Count > 0 ? errors : new GeoLocation(latitude, longitude);
    }

    private static void Validate(decimal value, decimal bound, Error outOfRange, Error tooPrecise, List<Error> errors)
    {
        if (value < -bound || value > bound)
        {
            errors.Add(outOfRange);
        }
        else if (decimal.Round(value, DecimalPlaces) != value)
        {
            errors.Add(tooPrecise);
        }
    }
}
