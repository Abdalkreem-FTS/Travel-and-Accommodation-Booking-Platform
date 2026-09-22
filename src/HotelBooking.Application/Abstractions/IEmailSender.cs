using HotelBooking.Domain.Common;

namespace HotelBooking.Application.Abstractions;

public sealed record EmailMessage(
    Email To,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
