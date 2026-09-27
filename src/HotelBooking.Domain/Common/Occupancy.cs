using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

public sealed record Occupancy
{
    public const int MinimumAdults = 1;

    public const int MaximumGuests = 10;

    private Occupancy(int adults, int children)
    {
        Adults = adults;
        Children = children;
    }

    public int Adults { get; }

    public int Children { get; }

    public int Total => Adults + Children;

    public static Result<Occupancy> Create(int adults, int children)
    {
        List<Error> errors = [];

        if (adults < MinimumAdults)
        {
            errors.Add(OccupancyErrors.AdultsRequired);
        }

        if (children < 0)
        {
            errors.Add(OccupancyErrors.ChildrenNegative);
        }

        if (errors.Count == 0 && adults + children > MaximumGuests)
        {
            errors.Add(OccupancyErrors.TooManyGuests);
        }

        return errors.Count > 0 ? errors : new Occupancy(adults, children);
    }

    public bool Accommodates(Occupancy guests) =>
        guests.Adults <= Adults && guests.Children <= Children;
}
