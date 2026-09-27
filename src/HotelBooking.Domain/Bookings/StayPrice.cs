using HotelBooking.Domain.Common;

namespace HotelBooking.Domain.Bookings;

public sealed record StayPrice(Money Total, Money Discount);
