using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Carts;

public interface ICartRepository
{
    Task<Result<Cart>> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<Success>> SaveItemAsync(
        Guid userId,
        CartItem item,
        CancellationToken cancellationToken = default);

    Task<Result<Success>> RemoveItemsAsync(
        Guid userId,
        IReadOnlyCollection<string> itemIds,
        CancellationToken cancellationToken = default);

    Task<Result<Deleted>> ClearAsync(Guid userId, CancellationToken cancellationToken = default);
}
