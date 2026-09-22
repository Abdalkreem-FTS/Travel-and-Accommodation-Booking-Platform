using FluentValidation;
using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Carts.Dtos;
using HotelBooking.Application.Common;
using HotelBooking.Domain.Carts;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;
using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Carts;

public sealed class CartService(
    ICartRepository cartRepository,
    IRoomRepository roomRepository,
    IDealRepository dealRepository,
    IDateTimeProvider dateTimeProvider,
    IValidator<AddCartItemRequest> validator,
    ILogger<CartService> logger) : ICartService
{
    public async Task<Result<CartDto>> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await cartRepository.GetAsync(userId, cancellationToken);

        if (cart.IsError)
        {
            return Degrade(cart, userId);
        }

        await PruneAsync(cart.Value, cancellationToken);

        return await PricedAsync(cart.Value, cancellationToken);
    }

    public async Task<Result<CartDto>> AddItemAsync(
        AddCartItemRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = await validator.ValidateToErrorsAsync(request, cancellationToken);

        if (validationErrors.Count > 0)
        {
            return validationErrors;
        }

        var stay = DateRange.Create(request.CheckIn, request.CheckOut, Today);
        var guests = Occupancy.Create(request.Adults, request.Children);

        List<Error> errors = [.. stay.Errors, .. guests.Errors];

        if (errors.Count > 0)
        {
            return errors;
        }

        var room = await roomRepository.GetByIdAsync(request.RoomId, cancellationToken);

        if (room is null)
        {
            return RoomErrors.NotFound;
        }

        var item = CartItem.For(room, stay.Value, guests.Value);

        if (item.IsError)
        {
            return item.Errors;
        }

        var cart = await cartRepository.GetAsync(userId, cancellationToken);

        if (cart.IsError)
        {
            return cart.Errors;
        }

        await PruneAsync(cart.Value, cancellationToken);

        var added = cart.Value.Add(item.Value);

        if (added.IsError)
        {
            return added.Errors;
        }

        var saved = await cartRepository.SaveItemAsync(userId, item.Value, cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        Telemetry.CartItemsAdded.Add(1);

        logger.LogInformation(
            "Held room {RoomId} for user {UserId} from {CheckIn} to {CheckOut}; the cart now holds {ItemCount}",
            room.Id, userId, stay.Value.CheckIn, stay.Value.CheckOut, cart.Value.Items.Count);

        return await PricedAsync(cart.Value, cancellationToken);
    }

    public async Task<Result<Deleted>> RemoveItemAsync(
        string itemId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var cart = await cartRepository.GetAsync(userId, cancellationToken);

        if (cart.IsError)
        {
            return cart.Errors;
        }

        var removed = cart.Value.Remove(itemId);

        if (removed.IsError)
        {
            return removed.Errors;
        }

        var saved = await cartRepository.RemoveItemsAsync(userId, [itemId], cancellationToken);

        return saved.IsError ? saved.Errors : Result.Deleted;
    }

    public async Task<Result<Deleted>> ClearAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cleared = await cartRepository.ClearAsync(userId, cancellationToken);

        if (cleared.IsError)
        {
            return cleared.Errors;
        }

        logger.LogInformation("Emptied the cart of user {UserId}", userId);

        return Result.Deleted;
    }

    private async Task<Result<CartDto>> PricedAsync(Cart cart, CancellationToken cancellationToken)
    {
        if (cart.Items.Count == 0)
        {
            return CartDto.From(cart, []);
        }

        var deals = await dealRepository.ListLiveForRoomsAsync(
            [.. cart.Items.Select(item => item.RoomId).Distinct()],
            cart.Items.Min(item => item.Stay.CheckIn),
            cart.Items.Max(item => item.Stay.CheckOut),
            cancellationToken);

        return CartDto.From(cart, deals);
    }

    private DateOnly Today => DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

    private async Task PruneAsync(Cart cart, CancellationToken cancellationToken)
    {
        var dropped = cart.Prune(Today);

        if (dropped.Count == 0)
        {
            return;
        }

        var removed = await cartRepository.RemoveItemsAsync(cart.UserId, dropped, cancellationToken);

        if (removed.IsError)
        {
            logger.LogWarning(
                "{DroppedCount} stay(s) in the cart of user {UserId} are no longer bookable but could "
                + "not be dropped from the store; they are hidden and will be dropped on a later read",
                dropped.Count, cart.UserId);

            return;
        }

        logger.LogInformation(
            "Dropped {DroppedCount} stay(s) from the cart of user {UserId}: check-in has passed",
            dropped.Count, cart.UserId);
    }

    private Result<CartDto> Degrade(Result<Cart> cart, Guid userId)
    {
        if (cart.TopError != CartErrors.Unavailable)
        {
            return cart.Errors;
        }

        logger.LogWarning(
            "The cart of user {UserId} could not be read and was served empty and degraded", userId);

        return CartDto.Unavailable();
    }
}
