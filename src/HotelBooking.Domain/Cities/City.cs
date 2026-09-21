using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Cities;

public sealed class City : AggregateRoot<Guid>
{
    public const int MaxNameLength = 100;

    public const int MaxPostOfficeLength = 20;

    public const int MaxThumbnailUrlLength = 2048;

    private City(
        Guid id,
        string name,
        CountryCode country,
        string postOffice,
        string? thumbnailUrl,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        Name = name;
        Country = country;
        PostOffice = postOffice;
        ThumbnailUrl = thumbnailUrl;
        CreatedAtUtc = createdAtUtc;
    }

    private City()
    {
        Name = null!;
        Country = null!;
        PostOffice = null!;
    }

    public string Name { get; private set; }

    public CountryCode Country { get; private set; }

    public string PostOffice { get; private set; }

    public string? ThumbnailUrl { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ModifiedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public static Result<City> Create(
        Guid id,
        string? name,
        CountryCode country,
        string? postOffice,
        string? thumbnailUrl,
        DateTimeOffset nowUtc)
    {
        var details = Validate(name, postOffice, thumbnailUrl);

        if (details.IsError)
        {
            return details.Errors;
        }

        return new City(id, details.Value.Name, country, details.Value.PostOffice, details.Value.ThumbnailUrl, nowUtc);
    }

    public Result<Updated> Update(
        string? name,
        CountryCode country,
        string? postOffice,
        string? thumbnailUrl,
        DateTimeOffset nowUtc)
    {
        if (IsDeleted)
        {
            return CityErrors.AlreadyDeleted;
        }

        var details = Validate(name, postOffice, thumbnailUrl);

        if (details.IsError)
        {
            return details.Errors;
        }

        Name = details.Value.Name;
        Country = country;
        PostOffice = details.Value.PostOffice;
        ThumbnailUrl = details.Value.ThumbnailUrl;
        ModifiedAtUtc = nowUtc;

        return Result.Updated;
    }

    public Result<Deleted> Delete(DateTimeOffset nowUtc)
    {
        if (IsDeleted)
        {
            return CityErrors.AlreadyDeleted;
        }

        IsDeleted = true;
        ModifiedAtUtc = nowUtc;

        return Result.Deleted;
    }

    private static Result<Details> Validate(string? name, string? postOffice, string? thumbnailUrl)
    {
        List<Error> errors = [];

        var trimmedName = TextField.Require(
            name, MaxNameLength, CityErrors.NameRequired, CityErrors.NameTooLong, errors);

        var trimmedPostOffice = TextField.Require(
            postOffice, MaxPostOfficeLength, CityErrors.PostOfficeRequired, CityErrors.PostOfficeTooLong, errors);

        var url = TextField.OptionalUrl(
            thumbnailUrl, MaxThumbnailUrlLength, CityErrors.ThumbnailUrlInvalid, errors);

        return errors.Count > 0 ? errors : new Details(trimmedName, trimmedPostOffice, url);
    }

    private readonly record struct Details(string Name, string PostOffice, string? ThumbnailUrl);
}
