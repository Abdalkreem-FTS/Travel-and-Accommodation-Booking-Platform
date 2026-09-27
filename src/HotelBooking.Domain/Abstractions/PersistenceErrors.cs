using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Abstractions;

public static class PersistenceErrors
{
    public static Error ConcurrencyConflict => Error.Conflict(
        "Persistence.ConcurrencyConflict",
        "This record was modified by another request. Reload it and try again.");
}
