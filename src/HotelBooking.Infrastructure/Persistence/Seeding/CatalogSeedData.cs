using System.Globalization;

using HotelBooking.Domain.Cities;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

namespace HotelBooking.Infrastructure.Persistence.Seeding;

internal static class CatalogSeedData
{
    private const string Currency = "USD";

    private static readonly RoomTemplate[] Layout =
    [
        new("101", RoomType.Budget, Adults: 1, Children: 0, Baseline: 75m),
        new("102", RoomType.Standard, Adults: 2, Children: 1, Baseline: 130m),
        new("201", RoomType.Standard, Adults: 2, Children: 2, Baseline: 150m),
        new("202", RoomType.Boutique, Adults: 2, Children: 1, Baseline: 250m),
        new("301", RoomType.Luxury, Adults: 2, Children: 2, Baseline: 450m)
    ];

    private static readonly string[] GalleryShots =
    [
        "The entrance at dusk", "A double room", "The breakfast room", "The view from the top floor"
    ];

    private static readonly string[] BaseAmenities = ["free-wifi", "parking", "air-conditioning"];

    private static readonly string[] MidAmenities = ["breakfast-included", "restaurant", "family-rooms"];

    private static readonly string[] TopAmenities =
        ["swimming-pool", "spa", "fitness-centre", "room-service"];

    private static readonly CitySpec[] CitySpecs =
    [
        new("amman", "Amman", "JO", "11118"),
        new("dubai", "Dubai", "AE", "PO Box 9292"),
        new("beirut", "Beirut", "LB", "1103"),
        new("jeddah", "Jeddah", "SA", "23218"),
        new("marrakesh", "Marrakesh", "MA", "40000"),
        new("cairo", "Cairo", "EG", "11511")
    ];

    private static readonly HotelSpec[] HotelSpecs =
    [
        new("amman", "qasr-al-jabal", "Qasr Al-Jabal", "Bilad Al-Sham Hospitality", 4,
            31.954722m, 35.933333m,
            "A quiet terrace hotel on the hill facing the Citadel, ten minutes on foot from the Roman "
            + "Theatre. Breakfast is served on the roof, where the view runs across the whole of old Amman."),
        new("amman", "bayt-al-hijara", "Bayt Al-Hijara", "Bilad Al-Sham Hospitality", 4,
            31.951389m, 35.929722m,
            "Sixteen rooms above the cafes and bookshops of Rainbow Street, in a restored 1940s stone "
            + "townhouse. Double glazing keeps the weekend crowds outside."),
        new("amman", "qasr-abdoun", "Qasr Abdoun", "Bilad Al-Sham Hospitality", 5,
            31.939444m, 35.885556m,
            "A full-service hotel in Abdoun with a spa, two restaurants and the largest conference floor "
            + "in the city. Popular with business travellers who want to be near the embassies."),
        new("amman", "nuzul-al-matar", "Nuzul Al-Matar", "Bilad Al-Sham Hospitality", 3,
            31.722556m, 35.993214m,
            "A straightforward, well-run stopover twelve minutes from the airport terminals, with a free "
            + "shuttle running around the clock and a kitchen that serves until midnight."),

        new("dubai", "lulu-al-marina", "Lulu Al-Marina", "Al-Khaleej Hotels", 5,
            25.077000m, 55.133000m,
            "Apartment-style suites over the marina walk, each with a kitchenette and a balcony facing "
            + "the water. The pool deck is on the twenty-eighth floor."),
        new("dubai", "khan-deira", "Khan Deira", "Al-Khaleej Hotels", 3,
            25.271000m, 55.309000m,
            "A plain, friendly hotel a street back from the gold and spice souks, with the abra crossing "
            + "to Bur Dubai five minutes away. Good value for the location."),
        new("dubai", "rimal-al-barsha", "Rimal Al-Barsha", "Al-Khaleej Hotels", 5,
            25.113000m, 55.196000m,
            "A resort built around three pools and a garden courtyard, with a children's club, four "
            + "restaurants and a shuttle to the beach every half hour."),
        new("dubai", "burj-al-wasat", "Burj Al-Wasat", "Al-Khaleej Hotels", 4,
            25.197197m, 55.274376m,
            "Modern suites in Downtown, a short walk from the fountain and the mall. Rooms on the high "
            + "floors look straight down the boulevard."),

        new("beirut", "manarat-al-rawsha", "Manarat Al-Rawsha", "Dar Al-Arz Hotels", 5,
            33.890000m, 35.471000m,
            "A corniche tower above the Rawsha rocks, where the sea-facing rooms watch the two stacks "
            + "from their balconies and the pool sits on the ninth floor."),
        new("beirut", "bayt-al-hamra", "Bayt Al-Hamra", "Dar Al-Arz Hotels", 4,
            33.895900m, 35.479500m,
            "A 1960s apartment building in Hamra kept as it was built, with terrazzo floors, a lobby "
            + "cafe that fills with students, and the university two streets away."),
        new("beirut", "nuzul-al-mina", "Nuzul Al-Mina", "Dar Al-Arz Hotels", 3,
            33.902000m, 35.519000m,
            "Small, plain rooms near the port, chosen mostly for the price and the walk into downtown. "
            + "The bakery on the corner opens at five."),

        new("jeddah", "dar-al-balad", "Dar Al-Balad", "Al-Hijaz Hotels", 4,
            21.482000m, 39.186000m,
            "A coral-stone merchant house in Al-Balad, restored around its courtyard, with carved "
            + "rawasheen shading every window and the old souk at the end of the lane."),
        new("jeddah", "shurfat-al-bahr", "Shurfat Al-Bahr", "Al-Hijaz Hotels", 5,
            21.580000m, 39.110000m,
            "A corniche hotel facing the fountain, with a private stretch of shore, four restaurants and "
            + "a terrace that stays busy until the small hours."),
        new("jeddah", "nuzul-al-hijaz", "Nuzul Al-Hijaz", "Al-Hijaz Hotels", 3,
            21.543300m, 39.172800m,
            "A pilgrims' stopover on the airport road, with a shuttle to both terminals, luggage storage "
            + "by the hour and a canteen that never really closes."),

        new("marrakesh", "dar-al-kutubiyya", "Dar Al-Kutubiyya", "Dar Al-Atlas Hotels", 5,
            31.623800m, -7.993000m,
            "A riad rebuilt as a small hotel in the shadow of the Kutubiyya minaret, with a plunge pool "
            + "in the courtyard and the Atlas visible from the roof on clear mornings."),
        new("marrakesh", "riad-al-bustan", "Riad Al-Bustan", "Dar Al-Atlas Hotels", 4,
            31.629500m, -7.981100m,
            "Eight rooms around an orange-tree courtyard in the medina, five minutes from Jemaa el-Fna "
            + "through lanes too narrow for cars. Porters meet guests at the gate."),
        new("marrakesh", "qasr-al-nakhil", "Qasr Al-Nakhil", "Dar Al-Atlas Hotels", 3,
            31.670000m, -7.950000m,
            "Low buildings among the palms of the Nakhil, twenty minutes out of the centre, with bicycles "
            + "to borrow and a pool that is empty most mornings."),

        new("cairo", "burj-al-nil", "Burj Al-Nil", "Al-Nil Heritage Hotels", 5,
            30.045000m, 31.229000m,
            "A tower on the Corniche with every room facing the river, a rooftop pool and a felucca "
            + "mooring of its own. The Egyptian Museum is a ten-minute walk."),
        new("cairo", "dar-al-zamalek", "Dar Al-Zamalek", "Al-Nil Heritage Hotels", 4,
            30.061000m, 31.220000m,
            "A converted villa on a quiet Zamalek street, with a mature garden, a dozen rooms and an "
            + "afternoon tea that regulars come back for."),
        new("cairo", "manzar-al-ahram", "Manzar Al-Ahram", "Al-Nil Heritage Hotels", 3,
            29.980000m, 31.134000m,
            "A family-run inn close enough to the plateau that the pyramids fill the roof terrace view. "
            + "Rooms are basic; the breakfast and the view are not.")
    ];

    private static readonly DealSpec[] DealSpecs =
    [
        new("qasr-abdoun", "301", 25),
        new("lulu-al-marina", "202", 20),
        new("dar-al-kutubiyya", "301", 15),
        new("burj-al-nil", "202", 30),
        new("qasr-al-jabal", "201", 10)
    ];

    public static SeededCatalog Build(DateTimeOffset nowUtc)
    {
        var ids = new Ids();

        var cities = CitySpecs.Select(spec => spec.ToCity(ids, nowUtc)).ToList();
        var hotels = HotelSpecs.Select(spec => spec.ToHotel(ids, nowUtc)).ToList();

        var rooms = HotelSpecs
            .SelectMany(hotel => Layout.Select(template => template.ToRoom(ids, hotel, nowUtc)))
            .ToList();

        var deals = DealSpecs.Select(spec => spec.ToDeal(ids, nowUtc)).ToList();

        return new SeededCatalog(cities, hotels, rooms, deals);
    }

    private sealed class Ids
    {
        private readonly Dictionary<string, Guid> _byKey = [];

        public Guid Of(string key) =>
            _byKey.TryGetValue(key, out var id) ? id : _byKey[key] = Guid.NewGuid();
    }

    private static Guid CityId(Ids ids, string slug) => ids.Of($"city:{slug}");

    private static Guid HotelId(Ids ids, string slug) => ids.Of($"hotel:{slug}");

    private static string Thumbnail(string slug) =>
        $"https://picsum.photos/seed/{slug}/800/600";

    private static IReadOnlyList<HotelImage> Gallery(string slug) =>
    [
        .. GalleryShots.Select((shot, index) => Required(
            HotelImage.Create($"https://picsum.photos/seed/{slug}-{index + 1}/1600/1000", shot),
            $"gallery image {index + 1} of {slug}"))
    ];

    private static IReadOnlyList<Guid> Amenities(int stars) =>
    [
        .. (stars switch
            {
                <= 3 => BaseAmenities,
                4 => [.. BaseAmenities, .. MidAmenities],
                _ => [.. BaseAmenities, .. MidAmenities, .. TopAmenities]
            })
            .Select(AmenityCatalogue.Id)
    ];

    private static Money PriceFor(decimal baseline, int stars)
    {
        var factor = stars switch
        {
            <= 3 => 0.85m,
            4 => 1.00m,
            _ => 1.20m
        };

        var amount = decimal.Round(baseline * factor * 2m, 0, MidpointRounding.AwayFromZero) / 2m;

        return Required(Money.Create(amount, Currency), $"price {amount}");
    }

    private static TValue Required<TValue>(Result<TValue> result, string what) =>
        result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The catalog seed data for '{what}' is invalid: {string.Join(", ", result.Errors.Select(error => error.Code))}."));

    private sealed record CitySpec(string Slug, string Name, string Country, string PostOffice)
    {
        public City ToCity(Ids ids, DateTimeOffset nowUtc) =>
            Required(
                City.Create(
                    CityId(ids, Slug),
                    Name,
                    Required(CountryCode.Create(Country), $"country {Country}"),
                    PostOffice,
                    Thumbnail(Slug),
                    nowUtc),
                $"city {Name}");
    }

    private sealed record HotelSpec(
        string CitySlug,
        string Slug,
        string Name,
        string Owner,
        int Stars,
        decimal Latitude,
        decimal Longitude,
        string Description)
    {
        public Hotel ToHotel(Ids ids, DateTimeOffset nowUtc) =>
            Required(
                Hotel.Create(
                    HotelId(ids, Slug),
                    CityId(ids, CitySlug),
                    Name,
                    Description,
                    Owner,
                    Required(StarRating.Create(Stars), $"star rating {Stars}"),
                    Required(GeoLocation.Create(Latitude, Longitude), $"location of {Name}"),
                    Thumbnail(Slug),
                    nowUtc,
                    Gallery(Slug),
                    Amenities(Stars)),
                $"hotel {Name}");
    }

    private sealed record DealSpec(string HotelSlug, string RoomNumber, int Percentage)
    {
        private const int WindowDays = 14;

        public Deal ToDeal(Ids ids, DateTimeOffset nowUtc)
        {
            var today = DateOnly.FromDateTime(nowUtc.UtcDateTime);

            return Required(
                Deal.Create(
                    ids.Of($"deal:{HotelSlug}:{RoomNumber}"),
                    HotelId(ids, HotelSlug),
                    ids.Of($"room:{HotelSlug}:{RoomNumber}"),
                    Required(DiscountPercentage.Create(Percentage), $"discount {Percentage}%"),
                    today.AddDays(-WindowDays),
                    today.AddDays(WindowDays),
                    isFeatured: true,
                    nowUtc),
                $"deal on room {RoomNumber} of {HotelSlug}");
        }
    }

    private sealed record RoomTemplate(string Number, RoomType Type, int Adults, int Children, decimal Baseline)
    {
        public Room ToRoom(Ids ids, HotelSpec hotel, DateTimeOffset nowUtc) =>
            Required(
                Room.Create(
                    ids.Of($"room:{hotel.Slug}:{Number}"),
                    HotelId(ids, hotel.Slug),
                    Number,
                    Type,
                    Required(Occupancy.Create(Adults, Children), $"occupancy {Adults}+{Children}"),
                    PriceFor(Baseline, hotel.Stars),
                    nowUtc),
                $"room {Number} of {hotel.Name}");
    }
}

internal sealed record SeededCatalog(
    IReadOnlyList<City> Cities,
    IReadOnlyList<Hotel> Hotels,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Deal> Deals);
