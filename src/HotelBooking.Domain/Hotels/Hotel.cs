using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Hotels;

public sealed class Hotel : AggregateRoot<Guid>
{
    public const int MaxNameLength = 200;

    public const int MaxDescriptionLength = 2000;

    public const int MaxOwnerLength = 200;

    public const int MaxThumbnailUrlLength = 2048;

    public const int MaxImages = 20;

    public const int MaxAmenities = 30;

    private readonly List<HotelImage> _images = [];

    private readonly List<HotelAmenityLink> _amenities = [];

    private Hotel(
        Guid id,
        Guid cityId,
        string name,
        string description,
        string owner,
        StarRating starRating,
        GeoLocation location,
        string? thumbnailUrl,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        CityId = cityId;
        Name = name;
        Description = description;
        Owner = owner;
        StarRating = starRating;
        Location = location;
        ThumbnailUrl = thumbnailUrl;
        CreatedAtUtc = createdAtUtc;
    }

    private Hotel()
    {
        Name = null!;
        Description = null!;
        Owner = null!;
        StarRating = null!;
        Location = null!;
    }

    public Guid CityId { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public string Owner { get; private set; }

    public StarRating StarRating { get; private set; }

    public GeoLocation Location { get; private set; }

    public string? ThumbnailUrl { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ModifiedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public IReadOnlyList<HotelImage> Images => _images;

    public IReadOnlyList<HotelAmenityLink> AmenityLinks => _amenities;

    public IReadOnlyList<Guid> AmenityIds => [.. _amenities.Select(link => link.AmenityId)];

    public static Result<Hotel> Create(
        Guid id,
        Guid cityId,
        string? name,
        string? description,
        string? owner,
        StarRating starRating,
        GeoLocation location,
        string? thumbnailUrl,
        DateTimeOffset nowUtc,
        IReadOnlyList<HotelImage>? images = null,
        IReadOnlyList<Guid>? amenityIds = null)
    {
        List<Error> errors = [];

        if (cityId == Guid.Empty)
        {
            errors.Add(HotelErrors.CityRequired);
        }

        var details = Validate(name, description, owner, thumbnailUrl, errors);
        var gallery = NumberGallery(images, errors);
        var links = DeduplicateAmenities(amenityIds, errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        var hotel = new Hotel(
            id, cityId, details.Name, details.Description, details.Owner, starRating, location, details.ThumbnailUrl, nowUtc);

        hotel._images.AddRange(gallery);
        hotel._amenities.AddRange(links);

        return hotel;
    }

    public Result<Updated> ReplaceGallery(IReadOnlyList<HotelImage> images, DateTimeOffset nowUtc)
    {
        if (IsDeleted)
        {
            return HotelErrors.AlreadyDeleted;
        }

        List<Error> errors = [];

        var gallery = NumberGallery(images, errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        _images.Clear();
        _images.AddRange(gallery);
        ModifiedAtUtc = nowUtc;

        return Result.Updated;
    }

    public Result<Updated> LinkAmenities(IReadOnlyList<Guid> amenityIds, DateTimeOffset nowUtc)
    {
        if (IsDeleted)
        {
            return HotelErrors.AlreadyDeleted;
        }

        List<Error> errors = [];

        var links = DeduplicateAmenities(amenityIds, errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        _amenities.Clear();
        _amenities.AddRange(links);
        ModifiedAtUtc = nowUtc;

        return Result.Updated;
    }

    public Result<Updated> Update(
        string? name,
        string? description,
        string? owner,
        StarRating starRating,
        GeoLocation location,
        string? thumbnailUrl,
        DateTimeOffset nowUtc)
    {
        if (IsDeleted)
        {
            return HotelErrors.AlreadyDeleted;
        }

        List<Error> errors = [];

        var details = Validate(name, description, owner, thumbnailUrl, errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        Name = details.Name;
        Description = details.Description;
        Owner = details.Owner;
        StarRating = starRating;
        Location = location;
        ThumbnailUrl = details.ThumbnailUrl;
        ModifiedAtUtc = nowUtc;

        return Result.Updated;
    }

    public Result<Updated> MoveTo(Guid cityId, DateTimeOffset nowUtc)
    {
        if (IsDeleted)
        {
            return HotelErrors.AlreadyDeleted;
        }

        if (cityId == Guid.Empty)
        {
            return HotelErrors.CityRequired;
        }

        CityId = cityId;
        ModifiedAtUtc = nowUtc;

        return Result.Updated;
    }

    public Result<Deleted> Delete(DateTimeOffset nowUtc)
    {
        if (IsDeleted)
        {
            return HotelErrors.AlreadyDeleted;
        }

        IsDeleted = true;
        ModifiedAtUtc = nowUtc;

        return Result.Deleted;
    }

    private static Details Validate(
        string? name,
        string? description,
        string? owner,
        string? thumbnailUrl,
        List<Error> errors)
    {
        var trimmedName = TextField.Require(
            name, MaxNameLength, HotelErrors.NameRequired, HotelErrors.NameTooLong, errors);

        var trimmedDescription = TextField.Require(
            description, MaxDescriptionLength, HotelErrors.DescriptionRequired, HotelErrors.DescriptionTooLong, errors);

        var trimmedOwner = TextField.Require(
            owner, MaxOwnerLength, HotelErrors.OwnerRequired, HotelErrors.OwnerTooLong, errors);

        var url = TextField.OptionalUrl(
            thumbnailUrl, MaxThumbnailUrlLength, HotelErrors.ThumbnailUrlInvalid, errors);

        return new Details(trimmedName, trimmedDescription, trimmedOwner, url);
    }

    private static List<HotelImage> NumberGallery(IReadOnlyList<HotelImage>? images, List<Error> errors)
    {
        if (images is null or { Count: 0 })
        {
            return [];
        }

        if (images.Count > MaxImages)
        {
            errors.Add(HotelErrors.TooManyImages);

            return [];
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        List<HotelImage> gallery = [];

        foreach (var image in images)
        {
            if (!seen.Add(image.Url))
            {
                errors.Add(HotelErrors.ImageAlreadyInGallery);

                return [];
            }

            gallery.Add(image.AtPosition(gallery.Count));
        }

        return gallery;
    }

    private static List<HotelAmenityLink> DeduplicateAmenities(
        IReadOnlyList<Guid>? amenityIds, List<Error> errors)
    {
        if (amenityIds is null or { Count: 0 })
        {
            return [];
        }

        HashSet<Guid> seen = [];
        List<HotelAmenityLink> links = [];

        foreach (var amenityId in amenityIds)
        {
            if (amenityId == Guid.Empty)
            {
                errors.Add(HotelErrors.AmenityRequired);

                return [];
            }

            if (seen.Add(amenityId))
            {
                links.Add(HotelAmenityLink.To(amenityId));
            }
        }

        if (links.Count <= MaxAmenities)
        {
            return links;
        }

        errors.Add(HotelErrors.TooManyAmenities);

        return [];
    }

    private readonly record struct Details(string Name, string Description, string Owner, string? ThumbnailUrl);
}
