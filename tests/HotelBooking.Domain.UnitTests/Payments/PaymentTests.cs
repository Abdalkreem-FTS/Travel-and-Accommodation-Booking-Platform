using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Payments;
using HotelBooking.Domain.Rooms;

namespace HotelBooking.Domain.UnitTests.Payments;

public sealed class PaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 25);

    private static readonly Money Total = Money.Create(241m, "USD").Value;

    private static Booking APendingBooking()
    {
        var room = Room.Create(
            Guid.NewGuid(), Guid.NewGuid(), "1203", RoomType.Luxury,
            Occupancy.Create(2, 0).Value, Money.Create(120.50m, "USD").Value, Now).Value;

        return Booking.Reserve(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new RoomStay(room, DateRange.Create(Today.AddDays(5), Today.AddDays(7), Today).Value, Occupancy.Create(2, 0).Value)],
            ConfirmationNumber.From(Guid.NewGuid()),
            Now).Value;
    }

    private static Payment APendingPayment() => Payment.Start(Guid.NewGuid(), APendingBooking(), Now).Value;

    private static Payment ASucceededPayment()
    {
        var payment = APendingPayment();

        payment.MarkSucceeded("pi_123", Total, Now.AddMinutes(5)).IsSuccess.ShouldBeTrue();

        return payment;
    }

    [Fact]
    public void Start_ForAPendingBooking_AsksForTheBookingTotalAndHoldsTheNightsForThirtyMinutes()
    {
        var payment = APendingPayment();

        payment.Status.ShouldBe(PaymentStatus.Pending);
        payment.Amount.ShouldBe(Total);
        payment.ExpiresAtUtc.ShouldBe(Now.AddMinutes(30));
    }

    [Fact]
    public void Start_ForABookingThatIsNotWaitingForPayment_IsRefused()
    {
        var booking = APendingBooking();
        booking.Expire().IsSuccess.ShouldBeTrue();

        Payment.Start(Guid.NewGuid(), booking, Now).TopError.ShouldBe(PaymentErrors.BookingNotPending);
    }

    [Fact]
    public void MarkSucceeded_ForADifferentAmount_IsRefusedRatherThanConfirmingAnUnderpaidStay()
    {
        var payment = APendingPayment();

        payment.MarkSucceeded("pi_123", Money.Create(1m, "USD").Value, Now).TopError.ShouldBe(PaymentErrors.AmountMismatch);

        payment.Status.ShouldBe(PaymentStatus.Pending);
    }

    [Fact]
    public void MarkSucceeded_AfterThePaymentExpired_IsRefusedBecauseItsNightsAreAlreadyReleased()
    {
        var payment = APendingPayment();
        payment.Expire(Now.AddMinutes(30)).IsSuccess.ShouldBeTrue();

        payment.MarkSucceeded("pi_123", Total, Now.AddMinutes(31)).TopError.ShouldBe(PaymentErrors.InvalidTransition);
    }

    [Fact]
    public void Expire_AfterThePaymentSucceeded_IsRefusedSoTheUnfinishedPaymentsWorkerNeverUndoesAPaidStay()
    {
        var payment = ASucceededPayment();

        payment.Expire(Now.AddMinutes(30)).TopError.ShouldBe(PaymentErrors.InvalidTransition);

        payment.Status.ShouldBe(PaymentStatus.Succeeded);
    }

    [Fact]
    public void RefundInFull_OnAPaymentThatTookNoMoney_IsRefused()
    {
        APendingPayment().RefundInFull(Now).TopError.ShouldBe(PaymentErrors.InvalidTransition);
    }

    [Fact]
    public void RefundInFull_NeverReturnsTheMoneyTwice()
    {
        var payment = ASucceededPayment();
        payment.RefundInFull(Now).IsSuccess.ShouldBeTrue();

        payment.RefundInFull(Now).TopError.ShouldBe(PaymentErrors.InvalidTransition);

        payment.CompleteRefund("re_123", Now).IsSuccess.ShouldBeTrue();

        payment.Status.ShouldBe(PaymentStatus.Refunded);
        payment.RefundInFull(Now).TopError.ShouldBe(PaymentErrors.InvalidTransition);
    }

    [Fact]
    public void RefundInFull_AfterTheProviderRejectedIt_IsNotAskedForAgain()
    {
        var payment = ASucceededPayment();
        payment.RefundInFull(Now).IsSuccess.ShouldBeTrue();

        payment.FailRefund(Now).IsSuccess.ShouldBeTrue();

        payment.Status.ShouldBe(PaymentStatus.RefundFailed);
        payment.RefundInFull(Now).TopError.ShouldBe(PaymentErrors.InvalidTransition, "a failed refund is returned by hand");
    }

    [Fact]
    public void AcceptLatePayment_OnAnExpiredPayment_HoldsTheMoneyOnlySoItCanBeRefunded()
    {
        var payment = APendingPayment();
        payment.Expire(Now.AddMinutes(30)).IsSuccess.ShouldBeTrue();

        payment.AcceptLatePayment("pi_123", Total, Now.AddMinutes(31)).IsSuccess.ShouldBeTrue();

        payment.RefundInFull(Now.AddMinutes(31)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void AcceptLatePayment_OnAPendingPayment_IsRefusedBecauseMoneyInTimeMustConfirmTheBooking()
    {
        APendingPayment().AcceptLatePayment("pi_123", Total, Now).TopError.ShouldBe(PaymentErrors.InvalidTransition);
    }
}
