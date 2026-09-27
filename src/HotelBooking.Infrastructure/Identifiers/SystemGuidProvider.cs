using HotelBooking.Application.Abstractions;

namespace HotelBooking.Infrastructure.Identifiers;

public sealed class SystemGuidProvider : IGuidProvider
{
    public Guid NewSortable() => Guid.CreateVersion7();

    public Guid NewOpaque() => Guid.NewGuid();
}
