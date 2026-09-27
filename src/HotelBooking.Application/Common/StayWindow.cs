using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Common;

public static class StayWindow
{
    public static Error Incomplete => Error.Validation(
        "Hotel.StayIncomplete",
        "checkOut",
        "Give both checkIn and checkOut to filter by availability, or neither to list everything.");

    public static DateRange? Read(
        DateOnly? checkIn,
        DateOnly? checkOut,
        DateOnly today,
        List<Error> errors)
    {
        if (checkIn is null && checkOut is null)
        {
            return null;
        }

        if (checkIn is not { } from || checkOut is not { } to)
        {
            errors.Add(Incomplete);

            return null;
        }

        var stay = DateRange.Create(from, to, today);

        errors.AddRange(stay.Errors);

        return stay.IsSuccess ? stay.Value : null;
    }
}
