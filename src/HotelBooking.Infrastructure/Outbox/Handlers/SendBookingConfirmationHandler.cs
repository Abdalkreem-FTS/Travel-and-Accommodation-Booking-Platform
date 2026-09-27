using System.Text.Json;

using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Bookings;
using HotelBooking.Domain.Bookings.Events;
using HotelBooking.Domain.Common;
using HotelBooking.Infrastructure.Notifications;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Infrastructure.Outbox.Handlers;

internal sealed class SendBookingConfirmationHandler(
    IBookingQueries bookings,
    IEmailSender email,
    ILogger<SendBookingConfirmationHandler> logger) : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string MessageType => nameof(BookingConfirmed);

    public async Task HandleAsync(OutboxEnvelope message, CancellationToken cancellationToken)
    {
        var confirmed = JsonSerializer.Deserialize<BookingConfirmed>(message.Content, SerializerOptions)
                        ?? throw new InvalidOperationException(
                            $"Outbox message {message.Id} does not carry a {MessageType}.");

        var booking = await bookings.GetConfirmationAsync(confirmed.BookingId, cancellationToken)
                      ?? throw new InvalidOperationException(
                          $"Booking {confirmed.BookingId} was confirmed but cannot be read back.");

        var recipient = Email.Create(booking.GuestEmail);

        if (recipient.IsError)
        {
            throw new InvalidOperationException(
                $"Booking {booking.BookingId} belongs to a guest whose address is not a valid email.");
        }

        await email.SendAsync(
            new EmailMessage(
                recipient.Value,
                booking.GuestName,
                BookingConfirmationEmail.Subject(booking),
                BookingConfirmationEmail.HtmlBody(booking),
                BookingConfirmationEmail.TextBody(booking)),
            cancellationToken);

        logger.LogInformation(
            "Sent the confirmation for booking {BookingId} ({ConfirmationNumber})",
            booking.BookingId, booking.ConfirmationNumber);
    }
}
