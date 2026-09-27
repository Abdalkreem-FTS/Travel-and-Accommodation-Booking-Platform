namespace HotelBooking.Application.Common;

public readonly record struct CacheKey(string Prefix, string Value)
{
    public override string ToString() => Value;
}
