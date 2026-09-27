namespace HotelBooking.Domain.Rooms;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Room>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsInHotelAsync(Guid hotelId, CancellationToken cancellationToken = default);

    void Add(Room room);
}
