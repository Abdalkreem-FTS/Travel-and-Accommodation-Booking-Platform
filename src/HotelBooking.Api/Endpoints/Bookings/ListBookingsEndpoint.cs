using System.Security.Claims;

using HotelBooking.Api.Authentication;
using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Bookings;
using HotelBooking.Application.Bookings.Dtos;
using HotelBooking.Application.Common;

namespace HotelBooking.Api.Endpoints.Bookings;

public sealed class ListBookingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/bookings", async (
                [AsParameters] BookingListRequest request,
                IBookingService bookingService,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                var result = await bookingService.ListAsync(request, user.GetUserId(), cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Bookings)
            .WithSummary("List your bookings")
            .WithDescription(
                "Every booking you have made, newest first, in every status (`Pending`, `Confirmed`, "
                + "`Cancelled`, `Expired`, ...). Each row names its hotel, even one removed from the "
                + "catalogue since, and counts its rooms; the lines and the payment are on "
                + "`GET /bookings/{id}`. Ties break on the id, so paging never drops or repeats a row. "
                + "\n\n"
                + $"Paged with `page` from 1 and `pageSize` up to {Pagination.MaximumPageSize} "
                + $"(default {Pagination.DefaultPageSize}). A guest with no bookings gets an empty page, "
                + "not `404`.")
            .Produces<PagedList<BookingSummaryDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization(Policy.AuthenticatedUser);
}
