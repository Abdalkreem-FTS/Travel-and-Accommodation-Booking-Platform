using System.Globalization;
using System.Net;
using System.Text;

using HotelBooking.Application.Bookings.Dtos;

namespace HotelBooking.Infrastructure.Notifications;

internal static class BookingConfirmationEmail
{
    public static string Subject(BookingConfirmationDto booking) =>
        $"Your booking at {booking.HotelName} is confirmed ({booking.ConfirmationNumber})";

    public static string TextBody(BookingConfirmationDto booking)
    {
        var body = new StringBuilder()
            .Append("Hello ").Append(booking.GuestName).AppendLine(",")
            .AppendLine()
            .Append("Your stay at ").Append(booking.HotelName)
            .Append(booking.CityName.Length == 0 ? string.Empty : $", {booking.CityName}")
            .AppendLine(" is confirmed.")
            .AppendLine()
            .Append("Confirmation number: ").AppendLine(booking.ConfirmationNumber)
            .Append("Check-in:  ").AppendLine(Date(booking.CheckIn))
            .Append("Check-out: ").AppendLine(Date(booking.CheckOut))
            .AppendLine()
            .AppendLine("Rooms");

        foreach (var line in booking.Lines)
        {
            body.Append("  ").Append(line.RoomType).Append(" room ").Append(line.RoomNumber)
                .Append(" — ").Append(Date(line.CheckIn)).Append(" to ").Append(Date(line.CheckOut))
                .Append(", ").Append(Nights(line.Nights))
                .Append(", ").Append(Guests(line))
                .Append(" — ").AppendLine(Amount(line.LineTotal, booking.Currency));
        }

        return body
            .AppendLine()
            .Append("Total: ").AppendLine(Amount(booking.TotalAmount, booking.Currency))
            .AppendLine()
            .AppendLine("You can cancel free of charge up to 48 hours before check-in.")
            .AppendLine()
            .AppendLine("Thank you for booking with us.")
            .ToString();
    }

    public static string HtmlBody(BookingConfirmationDto booking)
    {
        var rows = new StringBuilder();

        foreach (var line in booking.Lines)
        {
            rows.Append("<tr>")
                .Append("<td>").Append(Encode($"{line.RoomType} room {line.RoomNumber}")).Append("</td>")
                .Append("<td>").Append(Encode($"{Date(line.CheckIn)} – {Date(line.CheckOut)}")).Append("</td>")
                .Append("<td>").Append(Encode(Nights(line.Nights))).Append("</td>")
                .Append("<td>").Append(Encode(Guests(line))).Append("</td>")
                .Append("<td>").Append(Encode(Amount(line.LineTotal, booking.Currency))).Append("</td>")
                .Append("</tr>");
        }

        var location = booking.CityName.Length == 0
            ? Encode(booking.HotelName)
            : Encode($"{booking.HotelName}, {booking.CityName}");

        return $"""
                <!DOCTYPE html>
                <html lang="en">
                <body style="font-family:system-ui,sans-serif;color:#1a1a1a">
                  <p>Hello {Encode(booking.GuestName)},</p>
                  <p>Your stay at <strong>{location}</strong> is confirmed.</p>
                  <p>
                    Confirmation number: <strong>{Encode(booking.ConfirmationNumber)}</strong><br>
                    Check-in: {Encode(Date(booking.CheckIn))}<br>
                    Check-out: {Encode(Date(booking.CheckOut))}
                  </p>
                  <table cellpadding="6" cellspacing="0" border="0">
                    <thead>
                      <tr align="left">
                        <th>Room</th><th>Stay</th><th>Nights</th><th>Guests</th><th>Total</th>
                      </tr>
                    </thead>
                    <tbody>{rows}</tbody>
                  </table>
                  <p>Total: <strong>{Encode(Amount(booking.TotalAmount, booking.Currency))}</strong></p>
                  <p>You can cancel free of charge up to 48 hours before check-in.</p>
                  <p>Thank you for booking with us.</p>
                </body>
                </html>
                """;
    }

    private static string Date(DateOnly date) =>
        date.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

    private static string Nights(int nights) => nights == 1 ? "1 night" : $"{nights} nights";

    private static string Guests(BookingConfirmationLineDto line) =>
        line.Children == 0
            ? Plural(line.Adults, "adult")
            : $"{Plural(line.Adults, "adult")}, {Plural(line.Children, "child", "children")}";

    private static string Plural(int count, string singular, string? plural = null) =>
        count == 1 ? $"1 {singular}" : $"{count} {plural ?? singular + "s"}";

    private static string Amount(decimal amount, string currency) =>
        $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {currency}";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
