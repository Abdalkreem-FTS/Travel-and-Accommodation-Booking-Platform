using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Amenities;

public static class AmenityErrors
{
    public static Error SlugRequired => Error.Validation(
        "Amenity.SlugRequired", "slug", "An amenity needs a slug.");

    public static Error SlugTooLong => Error.Validation(
        "Amenity.SlugTooLong", "slug", $"A slug must be {Amenity.MaxSlugLength} characters or fewer.");

    public static Error SlugInvalid => Error.Validation(
        "Amenity.SlugInvalid",
        "slug",
        "A slug is lowercase letters, digits and single hyphens between them, such as free-wifi.");

    public static Error NameRequired => Error.Validation(
        "Amenity.NameRequired", "name", "An amenity needs a name.");

    public static Error NameTooLong => Error.Validation(
        "Amenity.NameTooLong", "name", $"A name must be {Amenity.MaxNameLength} characters or fewer.");

    public static Error DescriptionRequired => Error.Validation(
        "Amenity.DescriptionRequired", "description", "An amenity needs a description.");

    public static Error DescriptionTooLong => Error.Validation(
        "Amenity.DescriptionTooLong",
        "description",
        $"A description must be {Amenity.MaxDescriptionLength} characters or fewer.");

    public static Error UnknownAmenity => Error.Validation(
        "Amenity.NotFound", "amenityIds", "No amenity has that id.");
}
