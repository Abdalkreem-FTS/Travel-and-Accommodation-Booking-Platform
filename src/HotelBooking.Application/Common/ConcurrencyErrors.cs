using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Common;

public static class ConcurrencyErrors
{
    public static Error VersionRequired => Error.PreconditionRequired(
        "Concurrency.VersionRequired",
        "This edit needs an If-Match header carrying the version you read, so a colleague's "
        + "change cannot be overwritten unseen.");

    public static Error VersionMalformed => Error.BadRequest(
        "Concurrency.VersionMalformed",
        "The If-Match header is not a version this API issued.");
}
