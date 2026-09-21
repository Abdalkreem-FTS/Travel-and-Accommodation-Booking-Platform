using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Deals;

public static class DealErrors
{
    public static Error HotelRequired => Error.Validation(
        "Deal.HotelRequired", "hotelId", "A deal belongs to a hotel.");

    public static Error RoomRequired => Error.Validation(
        "Deal.RoomRequired", "roomId", "A deal discounts one room, so it needs that room's id.");

    public static Error WindowEndsBeforeItStarts => Error.Validation(
        "Deal.WindowEndsBeforeItStarts",
        "endsOn",
        "A deal must end after it starts, or it never runs.");

    public static Error WindowTooLong => Error.Validation(
        "Deal.WindowTooLong",
        "endsOn",
        $"A deal may run for at most {Deal.MaxNights} nights.");

    public static Error NotFound => Error.NotFound(
        "Deal.NotFound", "No such deal.");

    public static Error RoomNotFound => Error.Validation(
        "Deal.RoomNotFound", "roomId", "No room answers to that id.");

    public static Error AlreadyDeleted => Error.Conflict(
        "Deal.AlreadyDeleted", "This deal has already been deleted.");

    public static Error OverlapsExisting => Error.Conflict(
        "Deal.OverlapsExisting",
        "Another deal already discounts that room on one of those nights. A night carries at most "
        + "one deal, so end or move the existing one first.");
}
