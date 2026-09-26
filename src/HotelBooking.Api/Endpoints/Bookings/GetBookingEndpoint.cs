using System.Security.Claims;

using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Bookings;
using HotelBooking.Application.Bookings.Dtos;

namespace HotelBooking.Api.Endpoints.Bookings;

public sealed class GetBookingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/bookings/{bookingId:guid}", async (
                Guid bookingId,
                IBookingService bookingService,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                var result = await bookingService.GetAsync(bookingId, user.GetUserId(), cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Bookings)
            .WithSummary("Read one of your bookings")
            .WithDescription(
                "Returns a booking you own with its lines and its `payment`. This is what a client "
                + "polls after sending the guest to `payment.checkoutUrl`: the booking stays `Pending` "
                + "until the payment provider confirms the money, then becomes `Confirmed` (payment "
                + "`Succeeded`), or `Expired` (payment `Expired`) if the guest never paid before "
                + "`payment.expiresAtUtc`. Another guest's booking is `404`, never `403`, so booking "
                + "ids cannot be probed.")
            .Produces<BookingDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(Policy.AuthenticatedUser);
}
