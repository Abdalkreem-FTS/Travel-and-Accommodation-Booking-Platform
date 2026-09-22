using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Visits;

public static class VisitErrors
{
    public static Error RankingUnavailable => Error.Unavailable(
        "Visits.Unavailable", "The visit counters are unreachable.");
}
