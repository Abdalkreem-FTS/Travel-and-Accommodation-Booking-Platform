namespace HotelBooking.Domain.Abstractions;

public abstract class Entity<TId>(TId id)
    where TId : notnull
{
    protected Entity() : this(default!) { }

    public TId Id { get; private set; } = id;
}
