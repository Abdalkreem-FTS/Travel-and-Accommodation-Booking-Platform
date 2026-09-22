using System.Globalization;

using HotelBooking.Domain.Amenities;

namespace HotelBooking.Infrastructure.Persistence;

internal static class AmenityCatalogue
{
    private static readonly DateTimeOffset SeededAtUtc = new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    private static readonly (Guid Id, string Slug, string Name, string Description)[] Entries =
    [
        (new Guid("2ba4f778-b85a-4c3e-95f7-3cc6af85da0a"), "free-wifi", "Free Wi-Fi",
            "Wireless internet throughout the property, at no extra charge."),
        (new Guid("dd7e6adc-5a52-4f97-b3ae-f8728d04c0d3"), "parking", "Parking",
            "On-site parking for guests, free or paid."),
        (new Guid("9486e728-f6db-4ee8-8232-2f2480f5d098"), "swimming-pool", "Swimming Pool",
            "A pool guests may use, indoor or outdoor."),
        (new Guid("f530aac0-55f4-4536-88f8-35b39c9c3d6d"), "air-conditioning", "Air Conditioning",
            "Cooling a guest controls from the room."),
        (new Guid("95bc7403-568b-4e26-be5e-2ca051dd044e"), "breakfast-included", "Breakfast Included",
            "Breakfast served daily and included in the rate."),
        (new Guid("fd637415-faef-4f8f-bb7c-d84ed303ee9c"), "restaurant", "Restaurant",
            "At least one restaurant on the property."),
        (new Guid("c7846376-5c39-4dd3-8ebf-aad067cca5bb"), "fitness-centre", "Fitness Centre",
            "A gym guests may use during their stay."),
        (new Guid("de9a841f-14b6-4254-8f12-84ecacf9a359"), "spa", "Spa",
            "Spa or wellness treatments available on site."),
        (new Guid("05bb28ac-08c1-469e-a1ad-34b2bfdb5878"), "room-service", "Room Service",
            "Food and drink brought to the room."),
        (new Guid("acdd7355-8d42-401a-8a51-d2d53aa6fb90"), "airport-shuttle", "Airport Shuttle",
            "A shuttle between the property and the airport."),
        (new Guid("c6c1d494-dcf9-4d47-a677-5c10889c1230"), "family-rooms", "Family Rooms",
            "Rooms laid out for families travelling with children."),
        (new Guid("65714b0f-b41a-42a4-9f3a-b4c4180ee110"), "pet-friendly", "Pet Friendly",
            "Guests may bring pets, sometimes for a fee."),
        (new Guid("1d965abe-6bab-403c-8706-315c5ca91769"), "step-free-access", "Step-Free Access",
            "Entrance, reception and lifts reachable without steps.")
    ];

    private static readonly Dictionary<string, Guid> IdsBySlug =
        Entries.ToDictionary(entry => entry.Slug, entry => entry.Id);

    public static Guid Id(string slug) =>
        IdsBySlug.TryGetValue(slug, out var id)
            ? id
            : throw new InvalidOperationException($"No amenity is catalogued under the slug '{slug}'.");

    public static IReadOnlyList<Amenity> Build() =>
        [.. Entries.Select(entry => Create(entry.Id, entry.Slug, entry.Name, entry.Description))];

    private static Amenity Create(Guid id, string slug, string name, string description)
    {
        var amenity = Amenity.Create(id, slug, name, description, SeededAtUtc);

        return amenity.IsSuccess
            ? amenity.Value
            : throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The amenity catalogue entry '{slug}' is invalid: {string.Join(", ", amenity.Errors.Select(error => error.Code))}."));
    }
}
