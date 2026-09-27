using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Common;
using HotelBooking.Application.Rooms.Dtos;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Hotels;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

using Microsoft.Extensions.Logging;

namespace HotelBooking.Application.Rooms;

public sealed class RoomService(
    IRoomRepository roomRepository,
    IHotelRepository hotelRepository,
    IBookingRepository bookingRepository,
    IUnitOfWork unitOfWork,
    IConcurrencyGuard concurrencyGuard,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<RoomService> logger) : IRoomService
{
    public async Task<Result<RoomDto>> CreateAsync(
        Guid hotelId,
        CreateRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        var details = Describe(request.Adults, request.Children, request.BasePrice, request.Currency);

        if (details.IsError)
        {
            return details.Errors;
        }

        if (!await hotelRepository.ExistsAsync(hotelId, cancellationToken))
        {
            return HotelErrors.NotFound;
        }

        var room = Room.Create(
            guidProvider.NewSortable(),
            hotelId,
            request.Number,
            (RoomType)request.Type,
            details.Value.Capacity,
            details.Value.BasePrice,
            dateTimeProvider.UtcNow);

        if (room.IsError)
        {
            return room.Errors;
        }

        roomRepository.Add(room.Value);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        logger.LogInformation("Created room {RoomId} in hotel {HotelId}", room.Value.Id, hotelId);

        return RoomDto.From(room.Value, concurrencyGuard.TokenFor(room.Value));
    }

    public async Task<Result<RoomDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var room = await roomRepository.GetByIdAsync(id, cancellationToken);

        return room is null
            ? RoomErrors.NotFound
            : RoomDto.From(room, concurrencyGuard.TokenFor(room));
    }

    public async Task<Result<RoomDto>> UpdateAsync(
        Guid id,
        UpdateRoomRequest request,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var version = ConcurrencyToken.Create(ifMatch);

        if (version.IsError)
        {
            return version.Errors;
        }

        var details = Describe(request.Adults, request.Children, request.BasePrice, request.Currency);

        if (details.IsError)
        {
            return details.Errors;
        }

        var room = await roomRepository.GetByIdAsync(id, cancellationToken);

        if (room is null)
        {
            return RoomErrors.NotFound;
        }

        concurrencyGuard.Expect(room, version.Value);

        var updated = room.Update(
            request.Number,
            (RoomType)request.Type,
            details.Value.Capacity,
            details.Value.BasePrice,
            dateTimeProvider.UtcNow);

        if (updated.IsError)
        {
            return updated.Errors;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        logger.LogInformation("Updated room {RoomId}", id);

        return RoomDto.From(room, concurrencyGuard.TokenFor(room));
    }

    public async Task<Result<Deleted>> DeleteAsync(
        Guid id,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var version = ConcurrencyToken.Create(ifMatch);

        if (version.IsError)
        {
            return version.Errors;
        }

        var room = await roomRepository.GetByIdAsync(id, cancellationToken);

        if (room is null)
        {
            return RoomErrors.NotFound;
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        if (await bookingRepository.HasNightsFromAsync(id, today, cancellationToken))
        {
            Telemetry.CatalogDeletesRefused.Add(1, new KeyValuePair<string, object?>("resource", "room"));

            logger.LogInformation("Refused to delete room {RoomId} because it owes guests nights from {Today}", id, today);

            return RoomErrors.HasFutureBookings;
        }

        concurrencyGuard.Expect(room, version.Value);

        var deleted = room.Delete(dateTimeProvider.UtcNow);

        if (deleted.IsError)
        {
            return deleted.Errors;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (saved.IsError)
        {
            return saved.Errors;
        }

        logger.LogInformation("Deleted room {RoomId}", id);

        return Result.Deleted;
    }

    private static Result<Details> Describe(int adults, int children, decimal basePrice, string? currency)
    {
        var capacity = Occupancy.Create(adults, children);
        var price = Money.Create(basePrice, currency);

        List<Error> errors = [.. capacity.Errors, .. price.Errors];

        return errors.Count > 0 ? errors : new Details(capacity.Value, price.Value);
    }

    private sealed record Details(Occupancy Capacity, Money BasePrice);
}
