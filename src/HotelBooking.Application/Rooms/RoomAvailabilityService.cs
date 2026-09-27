using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Common;
using HotelBooking.Application.Rooms.Dtos;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Results;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Rooms;

public sealed class RoomAvailabilityService(
    IHotelRepository hotelRepository,
    IRoomQueries roomQueries,
    IDateTimeProvider dateTimeProvider,
    ILogger<RoomAvailabilityService> logger) : IRoomAvailabilityService
{
    public async Task<Result<PagedList<AvailableRoomDto>>> ListForHotelAsync(
        Guid hotelId,
        HotelRoomsRequest request,
        CancellationToken cancellationToken = default)
    {
        var criteria = RoomAvailabilityCriteria.Create(
            hotelId, request, DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime));

        if (criteria.IsError)
        {
            return criteria.Errors;
        }

        if (!await hotelRepository.ExistsAsync(hotelId, cancellationToken))
        {
            return HotelErrors.NotFound;
        }

        var page = await roomQueries.ListAvailableAsync(criteria.Value, cancellationToken);

        Telemetry.RoomAvailabilityQueries.Add(
            1, new KeyValuePair<string, object?>("dated", criteria.Value.Stay is not null));

        logger.LogDebug(
            "Listed rooms for hotel {HotelId} over {Stay}: {TotalCount} of {PageSize} per page matched",
            hotelId, criteria.Value.Stay?.ToString() ?? "any dates", page.TotalCount, page.PageSize);

        if (criteria.Value.Stay is not { } stay)
        {
            return page;
        }

        var quoted = Quote(page.Items, stay);

        return quoted.IsError
            ? quoted.Errors
            : page with { Items = quoted.Value };
    }

    private static Result<List<AvailableRoomDto>> Quote(IReadOnlyList<AvailableRoomDto> roomQueries, DateRange stay)
    {
        List<AvailableRoomDto> quoted = [];

        foreach (var room in roomQueries)
        {
            var nightly = Money.Create(room.NightlyRate, room.Currency);

            if (nightly.IsError)
            {
                return nightly.Errors;
            }

            var total = BookingPricingService.PriceStay(nightly.Value, stay);

            if (total.IsError)
            {
                return total.Errors;
            }

            quoted.Add(room with { Nights = stay.Nights, Total = total.Value.Amount });
        }

        return quoted;
    }
}
