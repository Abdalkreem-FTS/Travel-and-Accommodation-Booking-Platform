namespace HotelBooking.Application.Bookings.Dtos;

public sealed record BookingListRequest(int? Page = null, int? PageSize = null);
