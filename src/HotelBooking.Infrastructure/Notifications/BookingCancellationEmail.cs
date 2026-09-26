using System.Globalization;
using System.Net;
using System.Text;

using HotelBooking.Application.Bookings.Dtos;

namespace HotelBooking.Infrastructure.Notifications;

internal static class BookingCancellationEmail
{
    public static string Subject(BookingCancellationNoticeDto booking) =>
        $"Your booking at {booking.HotelName} is cancelled ({booking.ConfirmationNumber})";

    public static string TextBody(BookingCancellationNoticeDto booking) =>
        new StringBuilder()
            .Append("Hello ").Append(booking.GuestName).AppendLine(",")
            .AppendLine()
            .Append("Your stay at ").Append(Location(booking)).AppendLine(" is cancelled.")
            .AppendLine()
            .Append("Confirmation number: ").AppendLine(booking.ConfirmationNumber)
            .Append("Check-in:  ").AppendLine(Date(booking.CheckIn))
            .Append("Check-out: ").AppendLine(Date(booking.CheckOut))
            .AppendLine()
            .AppendLine(Money(booking))
            .AppendLine()
            .AppendLine("We hope to welcome you another time.")
            .ToString();

    public static string HtmlBody(BookingCancellationNoticeDto booking) =>
        $"""
         <!DOCTYPE html>
         <html lang="en">
         <body style="font-family:system-ui,sans-serif;color:#1a1a1a">
           <p>Hello {Encode(booking.GuestName)},</p>
           <p>Your stay at <strong>{Encode(Location(booking))}</strong> is cancelled.</p>
           <p>
             Confirmation number: <strong>{Encode(booking.ConfirmationNumber)}</strong><br>
             Check-in: {Encode(Date(booking.CheckIn))}<br>
             Check-out: {Encode(Date(booking.CheckOut))}
           </p>
           <p>{Encode(Money(booking))}</p>
           <p>We hope to welcome you another time.</p>
         </body>
         </html>
         """;

    private static string Money(BookingCancellationNoticeDto booking) =>
        booking.RefundAmount is { } refund
            ? $"A full refund of {Amount(refund, booking.Currency)} is on its way to the card you paid with. "
              + "Depending on your bank it can take a few days to appear."
            : "Nothing was charged for this booking, so there is nothing to refund.";

    private static string Location(BookingCancellationNoticeDto booking) =>
        booking.CityName.Length == 0 ? booking.HotelName : $"{booking.HotelName}, {booking.CityName}";

    private static string Date(DateOnly date) =>
        date.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

    private static string Amount(decimal amount, string currency) =>
        $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {currency}";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
