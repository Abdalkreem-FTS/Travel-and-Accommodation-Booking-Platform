using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Rooms;

public static class RoomErrors
{
    public static Error HotelRequired => Error.Validation(
        "Room.HotelRequired", "hotelId", "A room must belong to a hotel.");

    public static Error NumberRequired => Error.Validation(
        "Room.NumberRequired", "number", "Room number is required.");

    public static Error NumberTooLong => Error.Validation(
        "Room.NumberTooLong", "number", $"Room number must be {Room.MaxNumberLength} characters or fewer.");

    public static Error TypeInvalid => Error.Validation(
        "Room.TypeInvalid", "type", "That is not a known room type.");

    public static Error BasePriceMustBePositive => Error.Validation(
        "Room.BasePriceMustBePositive", "basePrice", "A room's nightly price must be greater than zero.");

    public static Error AlreadyDeleted => Error.Conflict(
        "Room.AlreadyDeleted", "This room has already been deleted.");

    public static Error NumberAlreadyUsedInHotel => Error.Conflict(
        "Room.NumberAlreadyUsedInHotel", "A room with that number already exists in that hotel.");

    public static Error HasFutureBookings => Error.Conflict(
        "Room.HasFutureBookings",
        "This room has nights sold today or later. Cancel those bookings before deleting it.");

    public static Error NotFound => Error.NotFound(
        "Room.NotFound", "No such room.");
}
