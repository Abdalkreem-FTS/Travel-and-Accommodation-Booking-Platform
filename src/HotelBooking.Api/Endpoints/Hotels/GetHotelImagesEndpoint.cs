using HotelBooking.Api.Errors;
using HotelBooking.Application.Hotels;
using HotelBooking.Application.Hotels.Dtos;

namespace HotelBooking.Api.Endpoints.Hotels;

public sealed class GetHotelImagesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/hotels/{hotelId:guid}/images", async (
                Guid hotelId,
                IHotelService hotelService,
                CancellationToken cancellationToken) =>
            {
                var result = await hotelService.GetGalleryAsync(hotelId, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Hotels)
            .WithSummary("List a hotel's gallery")
            .WithDescription(
                "The hotel's full gallery, in the order an administrator arranged it — `position` "
                + "is that order, counted from zero. A hotel with no photographs yet answers `200` "
                + "with an empty list; an unknown hotel is `404`. The same images are embedded in "
                + "the hotel itself, so a client drawing the detail page needs only that call.")
            .Produces<IReadOnlyList<HotelImageDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();
}
