using HotelBooking.Application.Bookings;
using HotelBooking.Application.Bookings.Dtos;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Queries;

internal sealed class BookingQueries(HotelBookingDbContext context) : IBookingQueries
{
    public async Task<BookingConfirmationDto?> GetConfirmationAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var row = await (
                from booking in context.Bookings.AsNoTracking().Where(booking => booking.Id == bookingId)
                join guest in context.Users.IgnoreQueryFilters()
                    on booking.UserId equals guest.Id
                join hotel in context.Hotels.IgnoreQueryFilters()
                    on booking.HotelId equals hotel.Id
                join city in context.Cities.IgnoreQueryFilters()
                    on hotel.CityId equals city.Id
                select new
                {
                    booking.Id,
                    booking.Confirmation,
                    booking.EarliestCheckIn,
                    booking.LatestCheckOut,
                    booking.TotalPrice.Amount,
                    booking.TotalPrice.Currency,
                    booking.CreatedAtUtc,
                    GuestEmail = guest.Email,
                    guest.FirstName,
                    guest.LastName,
                    HotelName = hotel.Name,
                    CityName = city.Name,
                    Lines = (
                        from line in booking.Lines
                        join room in context.Rooms.IgnoreQueryFilters()
                            on line.RoomId equals room.Id
                        orderby line.LineNumber
                        select new
                        {
                            line.LineNumber,
                            line.Stay.CheckIn,
                            line.Stay.CheckOut,
                            line.Guests.Adults,
                            line.Guests.Children,
                            line.LineTotal.Amount,
                            RoomNumber = room.Number,
                            RoomType = room.Type
                        })
                        .ToList()
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new BookingConfirmationDto(
            row.Id,
            row.Confirmation.Value,
            row.GuestEmail.Value,
            $"{row.FirstName} {row.LastName}",
            row.HotelName,
            row.CityName,
            row.EarliestCheckIn,
            row.LatestCheckOut,
            row.Amount,
            row.Currency,
            row.CreatedAtUtc,
            [
                .. row.Lines.Select(line => new BookingConfirmationLineDto(
                    line.LineNumber,
                    line.RoomNumber,
                    line.RoomType.ToString(),
                    line.CheckIn,
                    line.CheckOut,
                    line.CheckOut.DayNumber - line.CheckIn.DayNumber,
                    line.Adults,
                    line.Children,
                    line.Amount))
            ]);
    }
}
