using FluentValidation;

using HotelBooking.Application.Users.Dtos;
using HotelBooking.Domain.Common;

namespace HotelBooking.Application.Users.Validators;

public sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    private const int MinimumPasswordLength = 12;

    private const int MaximumPasswordLength = 128;

    public RegisterUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithErrorCode("Email.Required").WithMessage("Email is required.")
            .MaximumLength(Email.MaxLength).WithErrorCode("Email.TooLong")
            .WithMessage($"Email must be {Email.MaxLength} characters or fewer.");

        RuleFor(request => request.Password)
            .NotEmpty().WithErrorCode("Password.Required").WithMessage("Password is required.")
            .MinimumLength(MinimumPasswordLength).WithErrorCode("Password.TooShort")
            .WithMessage($"Password must be at least {MinimumPasswordLength} characters.")
            .MaximumLength(MaximumPasswordLength).WithErrorCode("Password.TooLong")
            .WithMessage($"Password must be {MaximumPasswordLength} characters or fewer.");

        RuleFor(request => request.FirstName)
            .NotEmpty().WithErrorCode("User.FirstNameRequired").WithMessage("First name is required.");

        RuleFor(request => request.LastName)
            .NotEmpty().WithErrorCode("User.LastNameRequired").WithMessage("Last name is required.");
    }
}
