using System.Security.Claims;

using HotelBooking.Api.Authentication;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Hotels;
using HotelBooking.Application.Hotels.Dtos;

namespace HotelBooking.Api.Endpoints.Hotels;

public sealed class GetHotelEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/hotels/{id:guid}", async (
                Guid id,
                IHotelService hotelService,
                ClaimsPrincipal user,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            {
                var viewerId = user.Identity?.IsAuthenticated is true ? user.GetUserId() : (Guid?)null;

                var clientAddress = response.HttpContext.Connection.RemoteIpAddress?.ToString();

                var result = await hotelService.GetAsync(id, viewerId, clientAddress, cancellationToken);

                return result.Match(
                    hotel => VersionedResults.Ok(response, hotel, hotel.Version),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Hotels)
            .WithSummary("Read a hotel")
            .WithDescription(
                "Returns one hotel with its gallery and the amenities it claims, plus the version "
                + "an administrator needs before editing it. `images` carry the order they are "
                + "meant to be shown in; `amenities` are catalogue entries shared with every other "
                + "hotel, not free text. The gallery is also its own sub-resource for clients that "
                + "want it alone, and availability for a stay is a question for the hotel's rooms. "
                + "Reviews are still to come."
                + "\n\n"
                + "Served from a cache for up to five minutes, dropped the moment an administrator "
                + "edits the hotel — so the `ETag` here is always one `If-Match` will accept."
                + "\n\n"
                + "Reading a hotel counts as a visit: it raises its city in the trending ranking "
                + "behind `GET /cities?sort=trending` — once per visitor per city per UTC day, the "
                + "visitor being the signed-in user or else the client address — and for a "
                + "signed-in caller it moves the "
                + "hotel to the front of `GET /viewed-hotels`. The counters live in Redis and are "
                + "best-effort — a hotel still reads normally when they cannot be written.")
            .Produces<HotelDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();
}
