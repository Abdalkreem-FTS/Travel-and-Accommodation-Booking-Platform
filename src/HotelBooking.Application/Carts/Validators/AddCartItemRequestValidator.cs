using FluentValidation;

using HotelBooking.Application.Carts.Dtos;

namespace HotelBooking.Application.Carts.Validators;

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(request => request.RoomId)
            .NotEmpty().WithErrorCode("Cart.RoomRequired").WithMessage("A room is required.");
    }
}
