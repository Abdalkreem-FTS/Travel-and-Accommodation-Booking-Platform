using HotelBooking.Domain.Results;

namespace HotelBooking.Application.Payments;

public static class PaymentErrors
{
    public static Error Declined => Error.PaymentRequired(
        "Payment.Declined",
        "Your payment was declined. Nothing has been booked - try another card and check out again.");

    public static Error AuthorizationFailed => Error.BadGateway(
        "Payment.AuthorizationFailed",
        "We could not reach the payment provider. Nothing has been booked or charged.");

    public static Error CaptureFailed => Error.BadGateway(
        "Payment.CaptureFailed",
        "We could not take the payment, so the booking was cancelled and nothing was charged. "
        + "Please check out again.");

    public static Error CaptureFailedAndNotReleased => Error.BadGateway(
        "Payment.CaptureFailedAndNotReleased",
        "We could not take the payment and could not cancel the booking automatically. Do not "
        + "check out again - contact support with your confirmation number.");
}
