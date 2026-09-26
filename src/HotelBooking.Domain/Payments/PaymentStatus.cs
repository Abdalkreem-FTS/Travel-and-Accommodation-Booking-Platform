namespace HotelBooking.Domain.Payments;

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Expired = 2,
    Refunding = 3,
    Refunded = 4,
    RefundFailed = 5
}
