using FluentValidation;

using HotelBooking.Application.Authentication.Dtos;

namespace HotelBooking.Application.Authentication.Validators;

public sealed class RefreshSessionRequestValidator : AbstractValidator<RefreshSessionRequest>
{
    public RefreshSessionRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty().WithErrorCode("Auth.RefreshTokenRequired").WithMessage("A refresh token is required.");
    }
}
