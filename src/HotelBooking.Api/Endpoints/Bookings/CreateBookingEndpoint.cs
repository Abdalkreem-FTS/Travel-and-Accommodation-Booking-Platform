using System.Security.Claims;

using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Bookings;
using HotelBooking.Application.Bookings.Dtos;

using Microsoft.AspNetCore.Mvc;

namespace HotelBooking.Api.Endpoints.Bookings;

public sealed class CreateBookingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/bookings", async (
                CreateBookingRequest? request,
                [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                IBookingService bookingService,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                var result = await bookingService.CreateAsync(
                    request ?? new CreateBookingRequest(null),
                    user.GetUserId(),
                    idempotencyKey,
                    cancellationToken);

                return result.Match(
                    booking => Results.Created($"/api/bookings/{booking.Id}", booking),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Bookings)
            .WithSummary("Check out one or more rooms")
            .WithDescription(
                "Reserves up to 10 stays at one hotel as a single booking, holds their nights, and "
                + "opens a hosted payment page for it. Send `items` to book exactly those stays, or "
                + "send no body at all to check out everything in your cart (`400 Cart.Empty` if "
                + "there is nothing bookable in it). An empty `items` list is "
                + "`400 Booking.ItemsRequired`, never a cart checkout. The cart only says which room "
                + "over which nights — every stay is priced again from the catalogue, so a stale cart "
                + "total never decides what you pay. Each stay becomes a line carrying that nightly "
                + "rate as a snapshot, and the reserved stays are dropped from the cart.\n\n"
                + "**Deals are applied night by night.** A night a live deal runs on is billed at "
                + "the discounted rate and a night it does not is billed at the room's own. The "
                + "line reports both: `nightlyRate` is what the room lists at, `discount` is what "
                + "the deals took off, and `lineTotal` is what is due.\n\n"
                + "**Paying.** The `201` carries the booking as `Pending` and a `payment` with a "
                + "`checkoutUrl`: send the guest there. The nights are held until the payment's "
                + "`expiresAtUtc`, 30 minutes from now; the booking is confirmed only once the "
                + "payment succeeds. A guest has at most one booking waiting for payment - another "
                + "checkout meanwhile is `409 Booking.PaymentPending`. If the payment page cannot be "
                + "opened, the booking is expired and its nights released before you are answered "
                + "`502 Payment.ProviderUnavailable`, so no room is held for a checkout nobody can "
                + "pay.\n\n"
                + "The whole booking is reserved or none of it is: one sold room-night refuses the "
                + "lot with `409 Booking.RoomUnavailable`. Requires an `Idempotency-Key` header: "
                + "the key is claimed before anything else is written, so a double-submitted "
                + "checkout is answered `409 Idempotency.RequestInProgress` rather than being told "
                + "the rooms it is trying to book are unavailable. Neither conflict is ever "
                + "retried server-side. Resending a key that already reserved a booking replays "
                + "that booking and its payment - the same `201` with the same ids - so a client "
                + "that lost the first answer can safely ask again.")
            .Produces<BookingDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(Policy.AuthenticatedUser);
}
