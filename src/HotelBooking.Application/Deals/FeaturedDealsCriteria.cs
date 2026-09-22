using HotelBooking.Application.Deals.Dtos;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Deals;

public sealed record FeaturedDealsCriteria(int Limit)
{
    public const int MinimumLimit = 1;

    public const int MaximumLimit = 10;

    public const int DefaultLimit = 5;

    public static Result<FeaturedDealsCriteria> Create(FeaturedDealsRequest request)
    {
        List<Error> errors = [];

        if (request.Featured is false)
        {
            errors.Add(FeaturedDealsErrors.UnfeaturedUnavailable);
        }

        var limit = ReadLimit(request.Limit, errors);

        return errors.Count > 0 ? errors : new FeaturedDealsCriteria(limit);
    }

    private static int ReadLimit(int? limit, List<Error> errors)
    {
        switch (limit)
        {
            case null:
                return DefaultLimit;
            case >= MinimumLimit and <= MaximumLimit:
                return limit.Value;
            default:
                errors.Add(FeaturedDealsErrors.LimitOutOfRange);

                return DefaultLimit;
        }
    }
}
