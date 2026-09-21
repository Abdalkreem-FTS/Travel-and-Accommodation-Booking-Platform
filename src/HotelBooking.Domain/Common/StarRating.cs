using System.Globalization;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

public sealed record StarRating
{
    public const int Minimum = 1;

    public const int Maximum = 5;

    private StarRating(int value) => Value = value;

    public int Value { get; }

    public static Result<StarRating> Create(int value) =>
        value is < Minimum or > Maximum
            ? StarRatingErrors.OutOfRange
            : new StarRating(value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
