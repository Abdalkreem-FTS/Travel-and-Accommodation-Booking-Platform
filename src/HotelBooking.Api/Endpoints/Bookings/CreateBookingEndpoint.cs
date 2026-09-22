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
                "Confirms up to 10 stays at one hotel as a single booking and sells their nights. "
                + "Send `items` to book exactly those stays, or send no body at all to check out "
                + "everything in your cart (`400 Cart.Empty` if there is nothing bookable in it). "
                + "An empty `items` list is `400 Booking.ItemsRequired`, never a cart checkout. "
                + "The cart only says which room over which nights — every stay is priced again "
                + "from the catalogue, so a stale cart total never decides what you are charged. "
                + "Each stay becomes a line carrying that nightly rate as a snapshot, so repricing "
                + "a room afterwards never changes what the guest was charged, and the stays that "
                + "were booked are dropped from the cart afterwards.\n\n"
                + "**Deals are applied night by night.** A night a live deal runs on is billed at "
                + "the discounted rate and a night it does not is billed at the room's own, so a "
                + "deal covering part of a stay discounts that part rather than being ignored. The "
                + "line reports both: `nightlyRate` is what the room lists at, `discount` is what "
                + "the deals took off, and `lineTotal` is what was charged.\n\n"
                + "The whole booking commits or none of it does: one sold room-night refuses the "
                + "lot with `409 Booking.RoomUnavailable`. Requires an `Idempotency-Key` header: "
                + "the key is claimed before anything else is written, so a double-submitted "
                + "checkout is answered `409 Idempotency.RequestInProgress` rather than being told "
                + "the rooms it is trying to book are unavailable. Neither conflict is ever "
                + "retried server-side.\n\n"
                + "Resending a key that already bought a booking replays that booking - the same "
                + "`201` with the same id and confirmation number - so a client that lost the "
                + "first answer can safely ask again without booking or paying twice. While the "
                + "first submission is still running there is no answer to replay yet, and the "
                + "retry gets `409 Idempotency.RequestInProgress`; try again shortly.\n\n"
                + "The card is authorized before the booking is written — a decline is "
                + "`402 Payment.Declined` and writes nothing — and captured after it commits. If "
                + "that capture fails, the booking is voided and its nights released before you "
                + "are answered `502 Payment.CaptureFailed`, so nothing is ever charged for a "
                + "booking you do not have, and no room is ever held for one.")
            .Produces<BookingDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(Policy.AuthenticatedUser);
}
