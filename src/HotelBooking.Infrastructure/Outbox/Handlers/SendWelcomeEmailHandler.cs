using System.Text.Json;

using HotelBooking.Application.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Users.Events;
using HotelBooking.Infrastructure.Notifications;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Infrastructure.Outbox.Handlers;

internal sealed class SendWelcomeEmailHandler(
    IEmailSender email,
    ILogger<SendWelcomeEmailHandler> logger) : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string MessageType => nameof(UserRegistered);

    public async Task HandleAsync(OutboxEnvelope message, CancellationToken cancellationToken)
    {
        var registered = JsonSerializer.Deserialize<UserRegistered>(message.Content, SerializerOptions)
                         ?? throw new InvalidOperationException(
                             $"Outbox message {message.Id} does not carry a {MessageType}.");

        var recipient = Email.Create(registered.Email);

        if (recipient.IsError)
        {
            throw new InvalidOperationException(
                $"User {registered.UserId} registered with an address that is not a valid email.");
        }

        await email.SendAsync(
            new EmailMessage(
                recipient.Value,
                registered.FirstName,
                WelcomeEmail.Subject,
                WelcomeEmail.HtmlBody(registered.FirstName),
                WelcomeEmail.TextBody(registered.FirstName)),
            cancellationToken);

        logger.LogInformation("Sent the welcome email for user {UserId}", registered.UserId);
    }
}
