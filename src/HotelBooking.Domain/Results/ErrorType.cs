namespace HotelBooking.Domain.Results;

public enum ErrorType
{
    Failure,
    Unexpected,
    Validation,
    BadRequest,
    Conflict,
    NotFound,
    Unauthorized,
    Forbidden,
    Unavailable,
    Timeout,
    TooManyRequests,
    PreconditionRequired,
    PreconditionFailed,
    PaymentRequired,
    BadGateway
}
