using FluentValidation;
using FluentValidation.Results;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Common;

public static class ValidationExtensions
{
    public static async Task<List<Error>> ValidateToErrorsAsync<TRequest>(
        this IValidator<TRequest> validator,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);

        return result.IsValid ? [] : [.. result.Errors.Select(ToError)];
    }

    private static Error ToError(ValidationFailure failure) => Error.Validation(
        string.IsNullOrWhiteSpace(failure.ErrorCode) ? Error.GenericValidationCode : failure.ErrorCode,
        ToCamelCase(failure.PropertyName),
        failure.ErrorMessage);

    private static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0])
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
