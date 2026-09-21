namespace HotelBooking.Domain.Deals;

public sealed class DealNight
{
    private DealNight(Guid roomId, DateOnly stayDate, Guid dealId)
    {
        RoomId = roomId;
        StayDate = stayDate;
        DealId = dealId;
    }

    private DealNight() { }

    public Guid RoomId { get; private set; }

    public DateOnly StayDate { get; private set; }

    public Guid DealId { get; private set; }

    internal static DealNight Claim(Guid roomId, DateOnly stayDate, Guid dealId) =>
        new(roomId, stayDate, dealId);
}
