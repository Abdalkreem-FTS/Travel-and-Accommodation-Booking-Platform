using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

public static class OccupancyErrors
{
    private const string AdultsField = "adults";

    private const string ChildrenField = "children";

    public static Error AdultsRequired => Error.Validation(
        "Occupancy.AdultsRequired", AdultsField, $"There must be at least {Occupancy.MinimumAdults} adult.");

    public static Error ChildrenNegative => Error.Validation(
        "Occupancy.ChildrenNegative", ChildrenField, "The number of children cannot be negative.");

    public static Error TooManyGuests => Error.Validation(
        "Occupancy.TooManyGuests", AdultsField, $"There may be at most {Occupancy.MaximumGuests} guests in total.");
}
