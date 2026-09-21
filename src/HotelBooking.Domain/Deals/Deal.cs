using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Deals;

public sealed class Deal : AggregateRoot<Guid>
{
    public const int MaxNights = 365;

    private readonly List<DealNight> _nights;

    private Deal(
        Guid id,
        Guid hotelId,
        Guid roomId,
        DiscountPercentage discount,
        DateOnly startsOn,
        DateOnly endsOn,
        bool isFeatured,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        HotelId = hotelId;
        RoomId = roomId;
        Discount = discount;
        StartsOn = startsOn;
        EndsOn = endsOn;
        IsFeatured = isFeatured;
        CreatedAtUtc = createdAtUtc;

        _nights = ClaimNights(id, roomId, startsOn, endsOn);
    }

    private Deal()
    {
        Discount = null!;
        _nights = [];
    }

    public Guid HotelId { get; private set; }

    public Guid RoomId { get; private set; }

    public DiscountPercentage Discount { get; private set; }

    public DateOnly StartsOn { get; private set; }

    public DateOnly EndsOn { get; private set; }

    public bool IsFeatured { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ModifiedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public IReadOnlyList<DealNight> Nights => _nights;

    public static Result<Deal> Create(
        Guid id,
        Guid hotelId,
        Guid roomId,
        DiscountPercentage discount,
        DateOnly startsOn,
        DateOnly endsOn,
        bool isFeatured,
        DateTimeOffset nowUtc)
    {
        List<Error> errors = [];

        if (hotelId == Guid.Empty)
        {
            errors.Add(DealErrors.HotelRequired);
        }

        if (roomId == Guid.Empty)
        {
            errors.Add(DealErrors.RoomRequired);
        }

        errors.AddRange(ValidateWindow(startsOn, endsOn));

        return errors.Count > 0
            ? errors
            : new Deal(id, hotelId, roomId, discount, startsOn, endsOn, isFeatured, nowUtc);
    }

    public Result<Updated> Update(
        DiscountPercentage discount,
        DateOnly startsOn,
        DateOnly endsOn,
        bool isFeatured,
        DateTimeOffset nowUtc)
    {
        if (IsDeleted)
        {
            return DealErrors.AlreadyDeleted;
        }

        var window = ValidateWindow(startsOn, endsOn);

        if (window.Count > 0)
        {
            return window;
        }

        Discount = discount;
        StartsOn = startsOn;
        EndsOn = endsOn;
        IsFeatured = isFeatured;
        ModifiedAtUtc = nowUtc;

        ReclaimNights(startsOn, endsOn);

        return Result.Updated;
    }

    public Result<Deleted> Delete(DateTimeOffset nowUtc)
    {
        if (IsDeleted)
        {
            return DealErrors.AlreadyDeleted;
        }

        IsDeleted = true;
        ModifiedAtUtc = nowUtc;

        _nights.Clear();

        return Result.Deleted;
    }

    public bool IsLiveOn(DateOnly date) => date >= StartsOn && date < EndsOn;

    private static List<Error> ValidateWindow(DateOnly startsOn, DateOnly endsOn)
    {
        if (endsOn <= startsOn)
        {
            return [DealErrors.WindowEndsBeforeItStarts];
        }

        return endsOn.DayNumber - startsOn.DayNumber > MaxNights
            ? [DealErrors.WindowTooLong]
            : [];
    }

    private void ReclaimNights(DateOnly startsOn, DateOnly endsOn)
    {
        _nights.RemoveAll(night => night.StayDate < startsOn || night.StayDate >= endsOn);

        var held = _nights.Select(night => night.StayDate).ToHashSet();

        for (var night = startsOn; night < endsOn; night = night.AddDays(1))
        {
            if (held.Add(night))
            {
                _nights.Add(DealNight.Claim(RoomId, night, Id));
            }
        }

        _nights.Sort((left, right) => left.StayDate.CompareTo(right.StayDate));
    }

    private static List<DealNight> ClaimNights(
        Guid id,
        Guid roomId,
        DateOnly startsOn,
        DateOnly endsOn)
    {
        List<DealNight> nights = [];

        for (var night = startsOn; night < endsOn; night = night.AddDays(1))
        {
            nights.Add(DealNight.Claim(roomId, night, id));
        }

        return nights;
    }
}
