using HotelBooking.Application.Carts.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Carts;

public interface ICartService
{
    Task<Result<CartDto>> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<CartDto>> AddItemAsync(
        AddCartItemRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<Deleted>> RemoveItemAsync(
        string itemId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<Deleted>> ClearAsync(Guid userId, CancellationToken cancellationToken = default);
}
