namespace HotelBooking.Application.Visits;

public interface IVisitTracker
{
    Task RecordHotelViewAsync(
        Guid hotelId,
        Guid cityId,
        Guid? viewerId,
        CancellationToken cancellationToken = default);

    Task ForgetCityAsync(Guid cityId, CancellationToken cancellationToken = default);
}
