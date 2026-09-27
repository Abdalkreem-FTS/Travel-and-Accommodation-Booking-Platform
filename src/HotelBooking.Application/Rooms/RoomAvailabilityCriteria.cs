using HotelBooking.Application.Common;
using HotelBooking.Application.Rooms.Dtos;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Rooms;

public sealed record RoomAvailabilityCriteria(
    Guid HotelId,
    DateRange? Stay,
    Occupancy Guests,
    int Page,
    int PageSize)
{
    private const int DefaultAdults = 2;

    private const int DefaultChildren = 0;

    public int Skip => (Page - Pagination.FirstPage) * PageSize;

    public static Result<RoomAvailabilityCriteria> Create(
        Guid hotelId,
        HotelRoomsRequest request,
        DateOnly today)
    {
        List<Error> errors = [];

        var stay = StayWindow.Read(request.CheckIn, request.CheckOut, today, errors);
        var paging = PageRequest.Read(request.Page, request.PageSize, errors);

        var guests = Occupancy.Create(
            request.Adults ?? DefaultAdults, request.Children ?? DefaultChildren);

        errors.AddRange(guests.Errors);

        return errors.Count > 0
            ? errors
            : new RoomAvailabilityCriteria(hotelId, stay, guests.Value, paging.Page, paging.PageSize);
    }
}
