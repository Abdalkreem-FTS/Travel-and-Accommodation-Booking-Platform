namespace HotelBooking.Application.Bookings.Dtos;

public sealed record CreateBookingRequest(IReadOnlyList<BookingItemRequest>? Items);
