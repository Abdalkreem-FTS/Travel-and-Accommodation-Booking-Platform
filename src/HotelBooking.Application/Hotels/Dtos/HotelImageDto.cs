using HotelBooking.Domain.Hotels;

namespace HotelBooking.Application.Hotels.Dtos;

public sealed record HotelImageDto(string Url, string? Caption, int Position)
{
    public static HotelImageDto From(HotelImage image) => new(image.Url, image.Caption, image.Position);
}
