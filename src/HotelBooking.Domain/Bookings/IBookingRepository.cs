namespace HotelBooking.Domain.Bookings;

public interface IBookingRepository
{
    void Add(Booking booking);

    Task<Booking?> GetWithNightsAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<Booking?> GetWithLinesAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> FindSoldRoomsAsync(
        IReadOnlyCollection<RoomStay> stays,
        CancellationToken cancellationToken = default);

    Task<bool> HasNightsFromAsync(
        Guid roomId,
        DateOnly fromDate,
        CancellationToken cancellationToken = default);
}
