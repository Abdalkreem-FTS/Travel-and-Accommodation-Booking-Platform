using FluentValidation;

using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Common;
using HotelBooking.Application.Users.Dtos;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Users;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Users;

public sealed class UserService(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    IValidator<RegisterUserRequest> validator,
    ILogger<UserService> logger) : IUserService
{
    public async Task<Result<UserDto>> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = await validator.ValidateToErrorsAsync(request, cancellationToken);

        if (validationErrors.Count > 0)
        {
            return validationErrors;
        }

        var email = Email.Create(request.Email);

        if (email.IsError)
        {
            return email.Errors;
        }

        if (await userRepository.ExistsWithEmailAsync(email.Value, cancellationToken))
        {
            return UserErrors.EmailAlreadyRegistered;
        }

        var user = User.Register(
            id: guidProvider.NewSortable(),
            eventId: guidProvider.NewSortable(),
            email: email.Value,
            passwordHash: passwordHasher.Hash(request.Password),
            firstName: request.FirstName,
            lastName: request.LastName,
            nowUtc: dateTimeProvider.UtcNow);

        if (user.IsError)
        {
            return user.Errors;
        }

        userRepository.Add(user.Value);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        Telemetry.UsersRegistered.Add(1);

        logger.LogInformation("Registered user {UserId}", user.Value.Id);

        return UserDto.From(user.Value);
    }

    public async Task<Result<UserDto>> FindAsync(
        UserSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = Email.Create(request.Email);

        if (email.IsError)
        {
            return email.Errors;
        }

        var user = await userRepository.GetByEmailAsync(email.Value, cancellationToken);

        return user is null ? UserErrors.NotFound : UserDto.From(user);
    }
}
