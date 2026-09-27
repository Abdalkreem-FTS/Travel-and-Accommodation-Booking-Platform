using HotelBooking.Api.Authorization;
using HotelBooking.Api.Errors;
using HotelBooking.Application.Common;
using HotelBooking.Application.Hotels;
using HotelBooking.Application.Hotels.Dtos;

namespace HotelBooking.Api.Endpoints.Cities;

public sealed class ListCityHotelsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/cities/{cityId:guid}/hotels", async (
                Guid cityId,
                [AsParameters] CityHotelsRequest request,
                IHotelService hotelService,
                CancellationToken cancellationToken) =>
            {
                var result = await hotelService.ListForCityAsync(cityId, request, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Cities)
            .WithSummary("List a city's hotels")
            .WithDescription(
                "Every hotel in the city, by name, **including hotels with no rooms yet**, which "
                + "`GET /hotels` never shows because a guest can't book them. `roomCount` says which "
                + "ones those are. Removed hotels are left out, and an unknown or removed city is `404`. "
                + "\n\n"
                + "`search` matches anywhere in the name and ignores case. Ties break on the id, so "
                + $"paging never drops or repeats a row. Paged with `page` from 1 and `pageSize` up to "
                + $"{Pagination.MaximumPageSize} (default {Pagination.DefaultPageSize}).")
            .Produces<PagedList<CityHotelDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(Policy.AdminOnly);
}
