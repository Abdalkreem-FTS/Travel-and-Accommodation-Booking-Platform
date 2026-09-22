namespace HotelBooking.Application.Amenities.Dtos;

public sealed record AmenityDto(Guid Id, string Slug, string Name, string Description);
