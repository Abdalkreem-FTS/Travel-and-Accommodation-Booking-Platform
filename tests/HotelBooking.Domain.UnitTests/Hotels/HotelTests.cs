using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.UnitTests.Hotels;

public sealed class HotelTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid HotelId = new("00000000-0000-0000-0003-000000000001");
    private static readonly Guid CityId = new("00000000-0000-0000-0002-000000000001");

    private static HotelImage AnImage(string url) => HotelImage.Create(url, null).Value;

    private static Result<Hotel> Create(IReadOnlyList<HotelImage>? images = null, IReadOnlyList<Guid>? amenityIds = null) =>
        Hotel.Create(
            HotelId, CityId, "Grand Plaza", "By the sea.", "Plaza Group",
            StarRating.Create(4).Value, GeoLocation.Create(31.95m, 35.93m).Value, null, Now, images, amenityIds);

    [Fact]
    public void Create_NumbersTheGalleryInTheOrderTheImagesWereGiven()
    {
        var hotel = Create(images: [AnImage("https://img.example/c.jpg"), AnImage("https://img.example/a.jpg")]).Value;

        hotel.Images.Select(image => (image.Url, image.Position)).ShouldBe(
            [("https://img.example/c.jpg", 0), ("https://img.example/a.jpg", 1)]);
    }

    [Fact]
    public void ReplaceGallery_WithTheSameUrlTwice_IsRefusedAndKeepsTheOldGallery()
    {
        var hotel = Create(images: [AnImage("https://img.example/a.jpg")]).Value;

        var result = hotel.ReplaceGallery(
            [AnImage("https://img.example/b.jpg"), AnImage("https://img.example/b.jpg")], Now);

        result.TopError.ShouldBe(HotelErrors.ImageAlreadyInGallery);
        hotel.Images.ShouldHaveSingleItem().Url.ShouldBe("https://img.example/a.jpg");
    }

    [Fact]
    public void Create_WithARepeatedAmenity_LinksItOnceButRefusesMoreThanTheCap()
    {
        var wifi = Guid.NewGuid();

        Create(amenityIds: [wifi, wifi]).Value.AmenityIds.ShouldBe([wifi]);

        Guid[] tooMany = [.. Enumerable.Range(0, Hotel.MaxAmenities + 1).Select(_ => Guid.NewGuid())];
        Create(amenityIds: tooMany).Errors.ShouldContain(HotelErrors.TooManyAmenities);
    }

    [Fact]
    public void Delete_LeavesAHotelThatRefusesEveryLaterChange()
    {
        var hotel = Create().Value;

        hotel.Delete(Now).IsSuccess.ShouldBeTrue();

        hotel.ShouldSatisfyAllConditions(
            () => hotel.Update("New", "New.", "New", hotel.StarRating, hotel.Location, null, Now)
                .TopError.ShouldBe(HotelErrors.AlreadyDeleted),
            () => hotel.MoveTo(Guid.NewGuid(), Now).TopError.ShouldBe(HotelErrors.AlreadyDeleted),
            () => hotel.ReplaceGallery([], Now).TopError.ShouldBe(HotelErrors.AlreadyDeleted),
            () => hotel.LinkAmenities([], Now).TopError.ShouldBe(HotelErrors.AlreadyDeleted),
            () => hotel.Delete(Now).TopError.ShouldBe(HotelErrors.AlreadyDeleted));
    }
}
