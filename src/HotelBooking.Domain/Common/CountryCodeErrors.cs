using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Common;

public static class CountryCodeErrors
{
    private const string Field = "country";

    public static Error Required => Error.Validation(
        "Country.Required", Field, "Country is required.");

    public static Error Invalid => Error.Validation(
        "Country.Invalid", Field, $"Country must be an ISO 3166-1 alpha-2 code of {CountryCode.Length} letters, for example JO.");
}
