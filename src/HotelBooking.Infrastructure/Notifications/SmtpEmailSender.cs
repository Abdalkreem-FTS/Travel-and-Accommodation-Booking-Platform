using HotelBooking.Application;
using HotelBooking.Application.Abstractions;

using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MimeKit;

namespace HotelBooking.Infrastructure.Notifications;

internal sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = Build(message);

        using var client = new SmtpClient();
        client.Timeout = (int)_options.Timeout.TotalMilliseconds;

        try
        {
            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);

            await client.DisconnectAsync(quit: true, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Telemetry.EmailsFailed.Add(1);

            logger.LogWarning(
                exception,
                "Could not send '{Subject}' to {Recipient} through {SmtpHost}:{SmtpPort}",
                message.Subject, Mask(message.To.Value), _options.Host, _options.Port);

            throw;
        }

        Telemetry.EmailsSent.Add(1);

        logger.LogInformation(
            "Sent '{Subject}' to {Recipient}", message.Subject, Mask(message.To.Value));
    }

    private MimeMessage Build(EmailMessage message)
    {
        var mime = new MimeMessage
        {
            Subject = message.Subject,
            Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }
                .ToMessageBody()
        };

        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.To.Value));

        return mime;
    }

    private static string Mask(string address)
    {
        var separator = address.IndexOf('@', StringComparison.Ordinal);

        return separator <= 0 ? "***" : $"{address[0]}***{address[separator..]}";
    }
}
