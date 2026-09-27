using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Idempotency;

public static class IdempotencyErrors
{
    public static Error KeyRequired => Error.PreconditionRequired(
        "Idempotency.KeyRequired", "This request requires an 'Idempotency-Key' header.");

    public static Error KeyTooLong => Error.Validation(
        "Idempotency.KeyTooLong",
        "idempotencyKey",
        $"An idempotency key must be {IdempotencyRecord.MaxKeyLength} characters or fewer.");

    public static Error RequestInProgress => Error.Conflict(
        "Idempotency.RequestInProgress",
        "A request with that idempotency key is already being processed.");

    public static Error ResultMissing => Error.Conflict(
        "Idempotency.ResultMissing",
        "That idempotency key belongs to a booking we can no longer read. Contact support rather "
        + "than checking out again.");
}
