using System.Security.Claims;

using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Bookings;
using HotelBooking.Application.Bookings.Dtos;

namespace HotelBooking.Api.Endpoints.Bookings;

public sealed class CancelBookingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/bookings/{bookingId:guid}/cancellation", async (
                Guid bookingId,
                IBookingCancellationService cancellationService,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                var result = await cancellationService.CancelAsync(
                    bookingId, user.GetUserId(), cancellationToken);

                return result.Match(
                    cancellation => Results.Created(
                        $"/api/bookings/{cancellation.BookingId}/cancellation", cancellation),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Bookings)
            .WithSummary("Cancel a booking")
            .WithDescription(
                "Cancels a booking you own and releases the room-nights it was holding, so the "
                + "rooms are immediately bookable by someone else. The status change, the deleted "
                + "ledger rows and the `BookingCancelled` message all commit in **one "
                + "transaction**: a cancelled booking never holds inventory, and a released room "
                + "is never freed without the booking that held it being cancelled.\n\n"
                + "Cancellation closes **48 hours before the earliest check-in** across the "
                + "booking's stays — after that it is `409 Booking.CancellationWindowClosed` and "
                + "the booking stands. A booking that is already cancelled, checked in or "
                + "completed is `409 Booking.InvalidTransition`, which is also what a replay of "
                + "this request gets, so no `Idempotency-Key` is needed. Two cancellations racing "
                + "each other resolve to one winner and one "
                + "`409 Persistence.ConcurrencyConflict`.\n\n"
                + "A booking belonging to another guest answers `404` rather than `403`, so this "
                + "endpoint cannot be used to discover which booking ids exist.\n\n"
                + "**A booking still waiting for payment** has its checkout closed at the payment "
                + "provider first, so the guest cannot pay for a stay they no longer hold. If they "
                + "paid at the last second the booking is kept and this is "
                + "`409 Booking.PaymentJustCompleted`; if the provider cannot be reached it is "
                + "`502 Payment.ProviderUnavailable` and nothing changes.\n\n"
                + "**A paid booking is refunded in full.** The refund is requested in the same "
                + "transaction and answered as `refund` with status `Pending`; the workers send "
                + "every pending refund to the payment provider within a minute, so a crash cannot "
                + "lose it. Read the booking to see it become `Succeeded`.")
            .Produces<BookingCancellationDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .RequireAuthorization(Policy.AuthenticatedUser);
}
