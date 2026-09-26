using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Payments;

public static class PaymentErrors
{
    public static Error BookingNotPending => Error.Conflict(
        "Payment.BookingNotPending", "Only a booking that is waiting for payment can be paid for.");

    public static Error InvalidTransition => Error.Conflict(
        "Payment.InvalidTransition", "That payment cannot move to that state from where it is.");

    public static Error CheckoutAlreadyAttached => Error.Conflict(
        "Payment.CheckoutAlreadyAttached", "That payment already has a checkout session.");

    public static Error AmountMismatch => Error.Conflict(
        "Payment.AmountMismatch", "The amount received is not the amount that was asked for.");

    public static Error ProviderUnavailable => Error.BadGateway(
        "Payment.ProviderUnavailable",
        "We could not reach the payment provider, so nothing was booked or charged. Please check out again.");

    public static Error EventSignatureInvalid => Error.BadRequest(
        "PaymentEvent.SignatureInvalid", "The payment event is not signed by the payment provider.");

    public static Error EventMalformed => Error.BadRequest(
        "PaymentEvent.Malformed", "The payment event could not be read.");

    public static Error RefundRejected => Error.Conflict(
        "Payment.RefundRejected", "The payment provider refused to return that money.");

    public static Error RefundNotFound => Error.NotFound(
        "Payment.RefundNotFound", "That payment has no refund.");
}
