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

    private static readonly Dictionary<string, string> CityPhotos = new()
    {
        ["amman"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2a/Jabel_Amman.jpg/1280px-Jabel_Amman.jpg",
        ["dubai"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e4/Dubai_skyline_unsplash.jpg/1280px-Dubai_skyline_unsplash.jpg",
        ["beirut"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/19/Pigeons_Rock%2C_Raouch%C3%A9%2C_Beirut_%282007%29.jpg/1280px-Pigeons_Rock%2C_Raouch%C3%A9%2C_Beirut_%282007%29.jpg",
        ["jeddah"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/7/7b/Old_Jeddah_%28Al_Balad%29%2C_Saudi_Arabia_in_November_2022.jpg/1280px-Old_Jeddah_%28Al_Balad%29%2C_Saudi_Arabia_in_November_2022.jpg",
        ["marrakesh"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/b/bd/Koutoubia_Mosque_1.jpg/1280px-Koutoubia_Mosque_1.jpg",
        ["cairo"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b2/View_from_Cairo_Tower_31march2007.jpg/1280px-View_from_Cairo_Tower_31march2007.jpg"
    };

    private static readonly Dictionary<string, string> HotelPhotos = new()
    {
        ["qasr-al-jabal"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/5/55/Exterior_of_the_Midland_Hotel%2C_Manchester%2C_UK_03.jpg/1280px-Exterior_of_the_Midland_Hotel%2C_Manchester%2C_UK_03.jpg",
        ["bayt-al-hijara"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/5/54/B2_Boutique_Hotel_%2B_Spa.jpg/1280px-B2_Boutique_Hotel_%2B_Spa.jpg",
        ["qasr-abdoun"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/d/da/The_Swallow_Hotel_And_Attached_Front_Entrance_Balustrades.jpg/1280px-The_Swallow_Hotel_And_Attached_Front_Entrance_Balustrades.jpg",
        ["nuzul-al-matar"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e1/Hotel_Mater_Boni_Consilii_main_building_at_Huye.jpg/1280px-Hotel_Mater_Boni_Consilii_main_building_at_Huye.jpg",
        ["lulu-al-marina"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2b/Hurghada_Hotels_Three_Corners_18.jpg/1280px-Hurghada_Hotels_Three_Corners_18.jpg",
        ["khan-deira"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Lebanon_hotel_swimming_pool.jpg/1280px-Lebanon_hotel_swimming_pool.jpg",
        ["rimal-al-barsha"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/e/ec/Vista_Cay_Resort%2C_Orlando_May_2023_a_swimming_pool_%282%29.jpg/1280px-Vista_Cay_Resort%2C_Orlando_May_2023_a_swimming_pool_%282%29.jpg",
        ["burj-al-wasat"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/3/39/The_WB_Abu_Dhabi%2C_Curio_Collection_By_Hilton_02.jpg/1280px-The_WB_Abu_Dhabi%2C_Curio_Collection_By_Hilton_02.jpg",
        ["manarat-al-rawsha"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c6/Ancien_grand_h%C3%B4tel_du_lac_%C3%A0_Hossegor.jpg/1280px-Ancien_grand_h%C3%B4tel_du_lac_%C3%A0_Hossegor.jpg",
        ["bayt-al-hamra"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/4/49/Grand_Hotel_Facade.JPG/1280px-Grand_Hotel_Facade.JPG",
        ["nuzul-al-mina"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d9/Hotel_facade%2C_Ibiza.jpg/1280px-Hotel_facade%2C_Ibiza.jpg",
        ["dar-al-balad"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/fd/Hurghada_Hotels_Three_Corners_13.jpg/1280px-Hurghada_Hotels_Three_Corners_13.jpg",
        ["shurfat-al-bahr"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c9/Hurghada_Hotels_Three_Corners_19.jpg/1280px-Hurghada_Hotels_Three_Corners_19.jpg",
        ["nuzul-al-hijaz"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/4/42/Hurghada_Hotels_Three_Corners_2.jpg/1280px-Hurghada_Hotels_Three_Corners_2.jpg",
        ["dar-al-kutubiyya"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b9/Hotel_Plaza_lobby_in_Havana.JPG/1280px-Hotel_Plaza_lobby_in_Havana.JPG",
        ["riad-al-bustan"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/d/df/Marrakech_riad.jpg/1280px-Marrakech_riad.jpg",
        ["qasr-al-nakhil"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/6/64/Swimming_pool_of_the_Berbere_Palace_Hotel%2C_Ouarzazate%2C_Morocco.jpg/1280px-Swimming_pool_of_the_Berbere_Palace_Hotel%2C_Ouarzazate%2C_Morocco.jpg",
        ["burj-al-nil"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/6/61/Hotel_and_palm_trees_%28Unsplash%29.jpg/1280px-Hotel_and_palm_trees_%28Unsplash%29.jpg",
        ["dar-al-zamalek"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f4/Grand_Hotel_2010.jpg/1280px-Grand_Hotel_2010.jpg",
        ["manzar-al-ahram"] =
            "https://upload.wikimedia.org/wikipedia/commons/thumb/5/59/Africana_Hotel%2C_Egypt.jpg/1280px-Africana_Hotel%2C_Egypt.jpg"
    };

    private static readonly GalleryShot[] GalleryShots =
    [
        new("The entrance at dusk",
        [
            "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2a/Chateau_Frontenac_at_dusk_in_Quebec_City.jpg/1280px-Chateau_Frontenac_at_dusk_in_Quebec_City.jpg",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/15/Beacon_Hotel_at_Night.jpg/1280px-Beacon_Hotel_at_Night.jpg",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c2/Ch%C3%A2teau_Frontenac_Hotel%2C_Quebec.jpg/1280px-Ch%C3%A2teau_Frontenac_Hotel%2C_Quebec.jpg",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/12/Grand_Hotel_Europe_NY_2008.JPG/1280px-Grand_Hotel_Europe_NY_2008.JPG"
        ]),
        new("A double room",
        [
            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/ff/Bed_in_hotel_room_2.jpg/1280px-Bed_in_hotel_room_2.jpg",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/4/4f/Bed_in_hotel_room_5.jpg/1280px-Bed_in_hotel_room_5.jpg",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a4/Room_Hotel_Sofitel_Strasbourg.jpg/1280px-Room_Hotel_Sofitel_Strasbourg.jpg",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/a/ae/Room_Hotel_Sofitel_Strasbourg_307.jpg/1280px-Room_Hotel_Sofitel_Strasbourg_307.jpg",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c1/Bed_at_Tianhe_Hotel_in_Shenzhen.jpg/1280px-Bed_at_Tianhe_Hotel_in_Shenzhen.jpg"
        ]),
        new("The breakfast room",
        [
            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/fa/Breakfast_room_in_Opera_Suite_Hotel_Yerevan_%28June_2023%29.JPG/1280px-Breakfast_room_in_Opera_Suite_Hotel_Yerevan_%28June_2023%29.JPG",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/7/79/Breakfast-buffet-in-Brazilian-hotel_08_04_05_674000.jpeg/1280px-Breakfast-buffet-in-Brazilian-hotel_08_04_05_674000.jpeg",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/fd/Argo_Hotel_interior_dining_room.jpg/1280px-Argo_Hotel_interior_dining_room.jpg",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/1d/Hurghada_Hotels_Three_Corners_5.jpg/1280px-Hurghada_Hotels_Three_Corners_5.jpg",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d7/Hurghada_Hotels_Three_Corners_6.jpg/1280px-Hurghada_Hotels_Three_Corners_6.jpg"
        ]),
        new("The view from the top floor",
        [
            "https://upload.wikimedia.org/wikipedia/commons/thumb/6/62/Hotel_National%2C_Moscow._View_from_the_upper_floor_room_%28Unsplash%29.jpg/1280px-Hotel_National%2C_Moscow._View_from_the_upper_floor_room_%28Unsplash%29.jpg",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c0/Amman_Night_Down_Town.JPG/1280px-Amman_Night_Down_Town.JPG",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/9/97/View_from_the_top_floor_of_SUST_IICT_Building.jpg/1280px-View_from_the_top_floor_of_SUST_IICT_Building.jpg",
            "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5d/Matanzas_Inlet_from_the_top_floor_of_Ft._Matanzas%2C_St._Augustine%2C_FL.jpg/1280px-Matanzas_Inlet_from_the_top_floor_of_Ft._Matanzas%2C_St._Augustine%2C_FL.jpg"
        ])
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
        var hotels = HotelSpecs.Select((spec, index) => spec.ToHotel(ids, index, nowUtc)).ToList();

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

    private static IReadOnlyList<HotelImage> Gallery(string slug, int hotelIndex) =>
    [
        .. GalleryShots.Select(shot => Required(
            HotelImage.Create(shot.Urls[hotelIndex % shot.Urls.Length], shot.Caption),
            $"'{shot.Caption}' of {slug}"))
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
                    CityPhotos[Slug],
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
        public Hotel ToHotel(Ids ids, int index, DateTimeOffset nowUtc) =>
            Required(
                Hotel.Create(
                    HotelId(ids, Slug),
                    CityId(ids, CitySlug),
                    Name,
                    Description,
                    Owner,
                    Required(StarRating.Create(Stars), $"star rating {Stars}"),
                    Required(GeoLocation.Create(Latitude, Longitude), $"location of {Name}"),
                    HotelPhotos[Slug],
                    nowUtc,
                    Gallery(Slug, index),
                    Amenities(Stars)),
                $"hotel {Name}");
    }

    private sealed record GalleryShot(string Caption, string[] Urls);

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
