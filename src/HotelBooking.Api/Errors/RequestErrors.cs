using HotelBooking.Domain.Results;

namespace HotelBooking.Api.Errors;

internal static class RequestErrors
{
    private static readonly Error NoSuchEndpoint = Error.NotFound(
        "Request.NoSuchEndpoint",
        "No endpoint matches this path.");

    private static readonly Error MethodNotAllowed = Error.BadRequest(
        "Request.MethodNotAllowed",
        "This endpoint does not support the request method.");

    private static readonly Error Unauthenticated = Error.Unauthorized(
        "Request.Unauthenticated",
        "This endpoint requires a valid access token.");

    private static readonly Error Forbidden = Error.Forbidden(
        "Request.Forbidden",
        "This account is not allowed to perform that action.");

    private static readonly Error NotAcceptable = Error.BadRequest(
        "Request.NotAcceptable",
        "This endpoint cannot produce any of the requested media types.");

    private static readonly Error UnsupportedMediaType = Error.BadRequest(
        "Request.UnsupportedMediaType",
        "The request body media type is not supported.");

    private static readonly Error Timeout = Error.Timeout(
        "Request.Timeout",
        "The request took too long to complete and was abandoned.");

    internal static readonly Error TooManyRequests = Error.TooManyRequests(
        "Request.TooManyRequests",
        "Too many attempts. Wait for the period given in the Retry-After header and try again.");

    internal static Error? ForFrameworkStatus(int? statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => Unauthenticated,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NoSuchEndpoint,
        StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
        StatusCodes.Status406NotAcceptable => NotAcceptable,
        StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaType,
        StatusCodes.Status504GatewayTimeout => Timeout,
        _ => null,
    };
}
