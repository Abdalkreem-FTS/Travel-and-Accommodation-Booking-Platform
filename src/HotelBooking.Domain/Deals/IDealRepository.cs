namespace HotelBooking.Domain.Deals;

public interface IDealRepository
{
    Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Deal>> ListLiveForRoomsAsync(
        IReadOnlyCollection<Guid> roomIds,
        DateOnly from,
        DateOnly until,
        CancellationToken cancellationToken = default);

    void Add(Deal deal);
}
