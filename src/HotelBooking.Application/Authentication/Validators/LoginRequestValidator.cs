using FluentValidation;

using HotelBooking.Application.Authentication.Dtos;

namespace HotelBooking.Application.Authentication.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithErrorCode("Email.Required").WithMessage("Email is required.");

        RuleFor(request => request.Password)
            .NotEmpty().WithErrorCode("Password.Required").WithMessage("Password is required.");
    }
}
