using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

// [in, out)
public sealed record DateRange
{
    public const int MaxNights = 30;

    private DateRange(DateOnly checkIn, DateOnly checkOut)
    {
        CheckIn = checkIn;
        CheckOut = checkOut;
    }

    public DateOnly CheckIn { get; }

    public DateOnly CheckOut { get; }

    public int Nights => CheckOut.DayNumber - CheckIn.DayNumber;

    public static Result<DateRange> Create(DateOnly checkIn, DateOnly checkOut, DateOnly today)
    {
        List<Error> errors = [];

        if (checkOut <= checkIn)
        {
            errors.Add(DateRangeErrors.CheckOutNotAfterCheckIn);
        }

        if (checkIn < today)
        {
            errors.Add(DateRangeErrors.CheckInInThePast);
        }

        if (errors.Count == 0 && checkOut.DayNumber - checkIn.DayNumber > MaxNights)
        {
            errors.Add(DateRangeErrors.TooManyNights);
        }

        return errors.Count > 0 ? errors : new DateRange(checkIn, checkOut);
    }

    public IEnumerable<DateOnly> EachNight()
    {
        for (var night = 0; night < Nights; night++)
        {
            yield return CheckIn.AddDays(night);
        }
    }

    public override string ToString() => $"{CheckIn:yyyy-MM-dd}..{CheckOut:yyyy-MM-dd}";
}
