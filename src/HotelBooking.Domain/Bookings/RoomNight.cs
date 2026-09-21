namespace HotelBooking.Domain.Bookings;

public sealed class RoomNight
{
    private RoomNight(Guid roomId, DateOnly stayDate, Guid bookingId)
    {
        RoomId = roomId;
        StayDate = stayDate;
        BookingId = bookingId;
    }

    private RoomNight() { }

    public Guid RoomId { get; private set; }

    public DateOnly StayDate { get; private set; }

    public Guid BookingId { get; private set; }

    internal static RoomNight Sell(Guid roomId, DateOnly stayDate, Guid bookingId) =>
        new(roomId, stayDate, bookingId);
}
