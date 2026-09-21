namespace HotelBooking.Domain.Hotels;

public interface IHotelRepository
{
    Task<Hotel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsInCityAsync(Guid cityId, CancellationToken cancellationToken = default);

    void Add(Hotel hotel);
}
