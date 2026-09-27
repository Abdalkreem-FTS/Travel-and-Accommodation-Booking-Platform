using System.Text.Json;

using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Bookings;
using HotelBooking.Domain.Bookings.Events;
using HotelBooking.Domain.Common;
using HotelBooking.Infrastructure.Notifications;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Infrastructure.Outbox.Handlers;

internal sealed class SendBookingCancellationHandler(
    IBookingQueries bookings,
    IEmailSender email,
    ILogger<SendBookingCancellationHandler> logger) : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string MessageType => nameof(BookingCancelled);

    public async Task HandleAsync(OutboxEnvelope message, CancellationToken cancellationToken)
    {
        var cancelled = JsonSerializer.Deserialize<BookingCancelled>(message.Content, SerializerOptions)
                        ?? throw new InvalidOperationException(
                            $"Outbox message {message.Id} does not carry a {MessageType}.");

        var booking = await bookings.GetCancellationNoticeAsync(cancelled.BookingId, cancellationToken)
                      ?? throw new InvalidOperationException(
                          $"Booking {cancelled.BookingId} was cancelled but cannot be read back.");

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
                BookingCancellationEmail.Subject(booking),
                BookingCancellationEmail.HtmlBody(booking),
                BookingCancellationEmail.TextBody(booking)),
            cancellationToken);

        logger.LogInformation(
            "Sent the cancellation for booking {BookingId} ({ConfirmationNumber})",
            booking.BookingId, booking.ConfirmationNumber);
    }
}
