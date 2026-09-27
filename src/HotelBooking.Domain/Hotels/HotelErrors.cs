using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Hotels;

public static class HotelErrors
{
    public static Error CityRequired => Error.Validation(
        "Hotel.CityRequired", "cityId", "A hotel must belong to a city.");

    public static Error CityNotFound => Error.Validation(
        "Hotel.CityNotFound", "cityId", "No city has that id.");

    public static Error NameRequired => Error.Validation(
        "Hotel.NameRequired", "name", "Hotel name is required.");

    public static Error NameTooLong => Error.Validation(
        "Hotel.NameTooLong", "name", $"Hotel name must be {Hotel.MaxNameLength} characters or fewer.");

    public static Error DescriptionRequired => Error.Validation(
        "Hotel.DescriptionRequired", "description", "Description is required.");

    public static Error DescriptionTooLong => Error.Validation(
        "Hotel.DescriptionTooLong", "description", $"Description must be {Hotel.MaxDescriptionLength} characters or fewer.");

    public static Error OwnerRequired => Error.Validation(
        "Hotel.OwnerRequired", "owner", "Owner is required.");

    public static Error OwnerTooLong => Error.Validation(
        "Hotel.OwnerTooLong", "owner", $"Owner must be {Hotel.MaxOwnerLength} characters or fewer.");

    public static Error ThumbnailUrlInvalid => Error.Validation(
        "Hotel.ThumbnailUrlInvalid", "thumbnailUrl", "Thumbnail must be an absolute http or https URL.");

    public static Error ImageUrlRequired => Error.Validation(
        "Hotel.ImageUrlRequired", "images", "Every gallery image needs a URL.");

    public static Error ImageUrlInvalid => Error.Validation(
        "Hotel.ImageUrlInvalid", "images", "A gallery image must be an absolute http or https URL.");

    public static Error ImageCaptionTooLong => Error.Validation(
        "Hotel.ImageCaptionTooLong",
        "images",
        $"An image caption must be {HotelImage.MaxCaptionLength} characters or fewer.");

    public static Error ImageAlreadyInGallery => Error.Validation(
        "Hotel.ImageAlreadyInGallery", "images", "The same image URL appears twice in the gallery.");

    public static Error TooManyImages => Error.Validation(
        "Hotel.TooManyImages", "images", $"A gallery holds at most {Hotel.MaxImages} images.");

    public static Error AmenityRequired => Error.Validation(
        "Hotel.AmenityRequired", "amenityIds", "An amenity id cannot be empty.");

    public static Error TooManyAmenities => Error.Validation(
        "Hotel.TooManyAmenities", "amenityIds", $"A hotel lists at most {Hotel.MaxAmenities} amenities.");

    public static Error AlreadyDeleted => Error.Conflict(
        "Hotel.AlreadyDeleted", "This hotel has already been deleted.");

    public static Error NameAlreadyUsedInCity => Error.Conflict(
        "Hotel.NameAlreadyUsedInCity", "A hotel with that name already exists in that city.");

    public static Error HasRooms => Error.Conflict(
        "Hotel.HasRooms", "This hotel still has rooms. Delete them before deleting the hotel.");

    public static Error NotFound => Error.NotFound(
        "Hotel.NotFound", "No such hotel.");
}
