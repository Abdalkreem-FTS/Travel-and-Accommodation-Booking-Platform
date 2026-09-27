using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Payments;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Payments;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace HotelBooking.Application.UnitTests.Payments;

public sealed class PaymentRefundServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 25);

    private readonly IPaymentProvider _provider = Substitute.For<IPaymentProvider>();
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly PaymentRefundService _service;

    private readonly Payment _payment;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public PaymentRefundServiceTests()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);

        var room = Room.Create(
            Guid.NewGuid(), Guid.NewGuid(), "1203", RoomType.Luxury,
            Occupancy.Create(2, 0).Value, Money.Create(120.50m, "USD").Value, Now).Value;

        var booking = Booking.Reserve(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new RoomStay(room, DateRange.Create(Today.AddDays(5), Today.AddDays(7), Today).Value, Occupancy.Create(2, 0).Value)],
            ConfirmationNumber.From(Guid.NewGuid()),
            Now).Value;

        _payment = Payment.Start(Guid.NewGuid(), booking, Now).Value;
        _payment.MarkSucceeded("pi_1", _payment.Amount, Now).IsSuccess.ShouldBeTrue();
        _payment.RefundInFull(Now).IsSuccess.ShouldBeTrue();

        _payments.GetAsync(_payment.Id, Arg.Any<CancellationToken>()).Returns(_payment);

        _service = new PaymentRefundService(
            _provider, _payments, _unitOfWork, clock, NullLogger<PaymentRefundService>.Instance);
    }

    [Fact]
    public async Task RefundAsync_WhenTheProviderRefusesForGood_RecordsTheRefundAsFailedAndStopsRetrying()
    {
        _provider.RefundAsync(_payment, Arg.Any<CancellationToken>()).Returns(PaymentErrors.RefundRejected);

        var result = await _service.RefundAsync(_payment.Id, Token);

        result.IsSuccess.ShouldBeTrue("retrying a refusal the provider will not change only delays the alarm");
        _payment.Status.ShouldBe(PaymentStatus.RefundFailed, "the money is still held and still owed");
    }

    [Fact]
    public async Task RefundAsync_WhenTheProviderCannotBeReached_LeavesTheRefundPendingForTheNextRun()
    {
        _provider.RefundAsync(_payment, Arg.Any<CancellationToken>())
            .Returns<Result<ProviderRefund>>(_ => throw new HttpRequestException("provider down"));

        var result = await _service.RefundAsync(_payment.Id, Token);

        result.IsError.ShouldBeTrue();
        _payment.Status.ShouldBe(PaymentStatus.Refunding);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefundAsync_ForARefundThatAlreadyEnded_NeverAsksTheProviderAgain()
    {
        _payment.CompleteRefund("re_1", Now).IsSuccess.ShouldBeTrue();

        var result = await _service.RefundAsync(_payment.Id, Token);

        result.IsSuccess.ShouldBeTrue();

        await _provider.DidNotReceive().RefundAsync(
            Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefundAsync_WhenTheProviderWillSettleItLater_KeepsItPendingButRemembersTheProvidersRefund()
    {
        _provider.RefundAsync(_payment, Arg.Any<CancellationToken>())
            .Returns(new ProviderRefund("re_1", Settled: false));

        var result = await _service.RefundAsync(_payment.Id, Token);

        result.IsSuccess.ShouldBeTrue("the refund is on its way, so the outbox must not send it again");
        _payment.Status.ShouldBe(PaymentStatus.Refunding);
        _payment.ProviderRefundId.ShouldBe("re_1");
    }
}
