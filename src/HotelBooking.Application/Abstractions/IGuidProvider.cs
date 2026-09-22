namespace HotelBooking.Application.Abstractions;

public interface IGuidProvider
{
    Guid NewSortable();

    Guid NewOpaque();
}
