using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Rooms;

public sealed class Room : AggregateRoot<Guid>
{
    public const int MaxNumberLength = 10;

    private Room(
        Guid id,
        Guid hotelId,
        string number,
        RoomType type,
        Occupancy capacity,
        Money basePrice,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        HotelId = hotelId;
        Number = number;
        Type = type;
        Capacity = capacity;
        BasePrice = basePrice;
        CreatedAtUtc = createdAtUtc;
    }

    private Room()
    {
        Number = null!;
        Capacity = null!;
        BasePrice = null!;
    }

    public Guid HotelId { get; private set; }

    public string Number { get; private set; }

    public RoomType Type { get; private set; }

    public Occupancy Capacity { get; private set; }

    public Money BasePrice { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ModifiedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public static Result<Room> Create(
        Guid id,
        Guid hotelId,
        string? number,
        RoomType type,
        Occupancy capacity,
        Money basePrice,
        DateTimeOffset nowUtc)
    {
        List<Error> errors = [];

        if (hotelId == Guid.Empty)
        {
            errors.Add(RoomErrors.HotelRequired);
        }

        var trimmedNumber = Validate(number, type, basePrice, errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        return new Room(id, hotelId, trimmedNumber, type, capacity, basePrice, nowUtc);
    }

    public bool CanHost(Occupancy guests) => !IsDeleted && Capacity.Accommodates(guests);

    public Result<Updated> Update(
        string? number,
        RoomType type,
        Occupancy capacity,
        Money basePrice,
        DateTimeOffset nowUtc)
    {
        if (IsDeleted)
        {
            return RoomErrors.AlreadyDeleted;
        }

        List<Error> errors = [];

        var trimmedNumber = Validate(number, type, basePrice, errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        Number = trimmedNumber;
        Type = type;
        Capacity = capacity;
        BasePrice = basePrice;
        ModifiedAtUtc = nowUtc;

        return Result.Updated;
    }

    public Result<Deleted> Delete(DateTimeOffset nowUtc)
    {
        if (IsDeleted)
        {
            return RoomErrors.AlreadyDeleted;
        }

        IsDeleted = true;
        ModifiedAtUtc = nowUtc;

        return Result.Deleted;
    }

    private static string Validate(string? number, RoomType type, Money basePrice, List<Error> errors)
    {
        var trimmedNumber = TextField.Require(
            number, MaxNumberLength, RoomErrors.NumberRequired, RoomErrors.NumberTooLong, errors);

        if (!Enum.IsDefined(type))
        {
            errors.Add(RoomErrors.TypeInvalid);
        }

        if (!basePrice.IsPositive)
        {
            errors.Add(RoomErrors.BasePriceMustBePositive);
        }

        return trimmedNumber;
    }
}
