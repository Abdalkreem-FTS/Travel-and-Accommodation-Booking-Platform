using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Cities;

public static class CityErrors
{
    public static Error NameRequired => Error.Validation(
        "City.NameRequired", "name", "City name is required.");

    public static Error NameTooLong => Error.Validation(
        "City.NameTooLong", "name", $"City name must be {City.MaxNameLength} characters or fewer.");

    public static Error PostOfficeRequired => Error.Validation(
        "City.PostOfficeRequired", "postOffice", "Post office is required.");

    public static Error PostOfficeTooLong => Error.Validation(
        "City.PostOfficeTooLong", "postOffice", $"Post office must be {City.MaxPostOfficeLength} characters or fewer.");

    public static Error ThumbnailUrlInvalid => Error.Validation(
        "City.ThumbnailUrlInvalid", "thumbnailUrl", "Thumbnail must be an absolute http or https URL.");

    public static Error AlreadyDeleted => Error.Conflict(
        "City.AlreadyDeleted", "This city has already been deleted.");

    public static Error NameAlreadyUsedInCountry => Error.Conflict(
        "City.NameAlreadyUsedInCountry", "A city with that name already exists in that country.");

    public static Error HasHotels => Error.Conflict(
        "City.HasHotels", "This city still has hotels. Delete or move them before deleting the city.");

    public static Error NotFound => Error.NotFound(
        "City.NotFound", "No such city.");
}
