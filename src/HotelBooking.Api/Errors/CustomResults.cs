using HotelBooking.Domain.Results;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

namespace HotelBooking.Api.Errors;

public static class CustomResults
{
    public static HttpResult Problem(List<Error> errors)
    {
        if (errors.Count == 0)
        {
            throw new ArgumentException("A failed result must carry at least one error.", nameof(errors));
        }

        return errors.TrueForAll(error => error.Type == ErrorType.Validation)
            ? ValidationProblem(errors)
            : SingleProblem(errors[0]);
    }

    public static HttpResult Problem(Error error) => SingleProblem(error);

    public static HttpResult ToProblem(this List<Error> errors) => Problem(errors);

    public static HttpResult ToProblem(this Error error) => SingleProblem(error);

    private static HttpResult SingleProblem(Error error) =>
        Results.Problem(
            title: ErrorProblemMapping.Title(error),
            detail: error.Description,
            statusCode: ErrorProblemMapping.StatusCode(error.Type),
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["errorCode"] = error.Code
            });

    private static HttpResult ValidationProblem(List<Error> errors)
    {
        var topError = errors[0];

        return Results.ValidationProblem(
            errors: ErrorProblemMapping.ValidationFailures(errors)!,
            detail: topError.Description,
            statusCode: StatusCodes.Status400BadRequest,
            title: ErrorProblemMapping.ValidationTitle,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["errorCode"] = topError.Code
            });
    }
}
