using HotelBooking.Api.Errors;
using HotelBooking.Application.Payments;

using Microsoft.AspNetCore.Mvc;

namespace HotelBooking.Api.Endpoints.Payments;

public sealed class ReceivePaymentEventEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/payment-events", async (
                HttpRequest request,
                [FromHeader(Name = "Stripe-Signature")] string? signature,
                IPaymentService paymentService,
                CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(request.Body);

                var payload = await reader.ReadToEndAsync(cancellationToken);

                var result = await paymentService.HandleEventAsync(payload, signature, cancellationToken);

                return result.Match(_ => Results.Ok(), CustomResults.Problem);
            })
            .WithTags(Tags.Payments)
            .WithSummary("Receive an event from the payment provider")
            .WithDescription(
                "Called by the payment provider, not by guests: there is no bearer token, and the body "
                + "is trusted only if its `Stripe-Signature` header proves the provider signed it "
                + "(`400 PaymentEvent.SignatureInvalid` otherwise).\n\n"
                + "A completed checkout marks the payment succeeded and confirms its booking, which "
                + "sends the confirmation email; an expired checkout expires both and releases the "
                + "booking's room-nights. Each event is applied **once**: its id is recorded in the "
                + "same transaction as its effect, so a redelivery is answered `200` and changes "
                + "nothing. An event that no longer applies - an expiry for a payment that already "
                + "succeeded - is also `200`, as is an event type this API does not handle.\n\n"
                + "Anything else non-2xx makes the provider deliver the event again later, which is "
                + "what a transient database failure needs.")
            .Accepts<object>("application/json")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AllowAnonymous();
}
