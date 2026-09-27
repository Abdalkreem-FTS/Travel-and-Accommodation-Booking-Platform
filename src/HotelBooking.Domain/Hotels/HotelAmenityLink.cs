namespace HotelBooking.Domain.Hotels;

public sealed record HotelAmenityLink
{
    private HotelAmenityLink(Guid amenityId) => AmenityId = amenityId;

    public Guid AmenityId { get; private set; }

    internal static HotelAmenityLink To(Guid amenityId) => new(amenityId);
}
