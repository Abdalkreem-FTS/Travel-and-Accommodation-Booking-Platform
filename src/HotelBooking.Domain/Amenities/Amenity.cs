using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Amenities;

public sealed class Amenity : AggregateRoot<Guid>
{
    public const int MaxSlugLength = 60;

    public const int MaxNameLength = 100;

    public const int MaxDescriptionLength = 400;

    private Amenity(Guid id, string slug, string name, string description, DateTimeOffset createdAtUtc)
        : base(id)
    {
        Slug = slug;
        Name = name;
        Description = description;
        CreatedAtUtc = createdAtUtc;
    }

    private Amenity()
    {
        Slug = null!;
        Name = null!;
        Description = null!;
    }

    public string Slug { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public static Result<Amenity> Create(
        Guid id,
        string? slug,
        string? name,
        string? description,
        DateTimeOffset nowUtc)
    {
        List<Error> errors = [];

        var normalisedSlug = NormaliseSlug(slug, errors);

        var trimmedName = TextField.Require(
            name,
            MaxNameLength,
            AmenityErrors.NameRequired,
            AmenityErrors.NameTooLong,
            errors);

        var trimmedDescription = TextField.Require(
            description,
            MaxDescriptionLength,
            AmenityErrors.DescriptionRequired,
            AmenityErrors.DescriptionTooLong,
            errors);

        return errors.Count > 0
            ? errors
            : new Amenity(id, normalisedSlug, trimmedName, trimmedDescription, nowUtc);
    }

    private static string NormaliseSlug(string? slug, List<Error> errors)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            errors.Add(AmenityErrors.SlugRequired);

            return string.Empty;
        }

        var normalised = slug.Trim().ToLowerInvariant();

        if (normalised.Length > MaxSlugLength)
        {
            errors.Add(AmenityErrors.SlugTooLong);
        }
        else if (!IsKebabCase(normalised))
        {
            errors.Add(AmenityErrors.SlugInvalid);
        }

        return normalised;
    }

    private static bool IsKebabCase(string value) =>
        value[0] != '-'
        && value[^1] != '-'
        && !value.Contains("--", StringComparison.Ordinal)
        && value.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');
}
