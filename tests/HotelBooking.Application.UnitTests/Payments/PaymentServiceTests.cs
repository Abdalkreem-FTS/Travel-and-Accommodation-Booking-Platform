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

public sealed class PaymentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 25);

    private static readonly Guid UserId = new("00000000-0000-0000-0001-000000000001");

    private const string CheckoutId = "cs_123";

    private readonly IPaymentProvider _provider = Substitute.For<IPaymentProvider>();
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly InlineUnitOfWork _unitOfWork = new();
    private readonly PaymentService _service;

    private readonly Booking _booking;
    private readonly Payment _payment;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public PaymentServiceTests()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var guids = Substitute.For<IGuidProvider>();
        guids.NewSortable().Returns(_ => Guid.NewGuid());

        var room = Room.Create(
            Guid.NewGuid(), Guid.NewGuid(), "1203", RoomType.Luxury,
            Occupancy.Create(2, 0).Value, Money.Create(120.50m, "USD").Value, Now).Value;

        _booking = Booking.Reserve(
            Guid.NewGuid(),
            UserId,
            [new RoomStay(room, DateRange.Create(Today.AddDays(5), Today.AddDays(7), Today).Value, Occupancy.Create(2, 0).Value)],
            ConfirmationNumber.From(Guid.NewGuid()),
            Now).Value;

        _payment = Payment.Start(Guid.NewGuid(), _booking, Now).Value;
        _payment.AttachCheckout(CheckoutId, "https://checkout.example/pay/cs_123").IsSuccess.ShouldBeTrue();

        _payments.GetByCheckoutIdAsync(CheckoutId, Arg.Any<CancellationToken>()).Returns(_payment);
        _bookings.GetWithNightsAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns(_booking);

        _service = new PaymentService(
            _provider, _payments, _bookings, _unitOfWork, guids, clock, NullLogger<PaymentService>.Instance);
    }

    private void GivenTheProviderSends(PaymentEvent paymentEvent) =>
        _provider.ReadEvent(Arg.Any<string>(), Arg.Any<string?>()).Returns(paymentEvent);

    [Fact]
    public async Task HandleEventAsync_MoneyForAPaymentThatAlreadyExpired_IsRefundedWithoutConfirmingTheReleasedBooking()
    {
        _payment.Expire(Now).IsSuccess.ShouldBeTrue();
        _booking.Expire().IsSuccess.ShouldBeTrue();

        GivenTheProviderSends(new CheckoutCompleted("evt_1", CheckoutId, "pi_1", _payment.Amount));

        var result = await _service.HandleEventAsync("{}", "signature", Token);

        result.IsSuccess.ShouldBeTrue("an error would make the provider retry an event no retry can fix");
        _booking.Status.ShouldBe(BookingStatus.Expired, "its nights may already belong to someone else");
        _payment.Status.ShouldBe(PaymentStatus.Refunding, "the guest gets the money back");
    }

    [Fact]
    public async Task HandleEventAsync_AnEventDeliveredAgain_FindsItsChangeMadeAndConfirmsNothingTwice()
    {
        var completed = new CheckoutCompleted("evt_1", CheckoutId, "pi_1", _payment.Amount);
        GivenTheProviderSends(completed);

        (await _service.HandleEventAsync("{}", "signature", Token)).IsSuccess.ShouldBeTrue();
        _booking.ClearDomainEvents();

        var again = await _service.HandleEventAsync("{}", "signature", Token);

        again.IsSuccess.ShouldBeTrue("a redelivery must not make the provider retry forever");
        _booking.DomainEvents.ShouldBeEmpty("a second BookingConfirmed would send a second email");
        _unitOfWork.Saves.ShouldBe(1, "the redelivery found nothing to write");
    }

    [Fact]
    public async Task HandleEventAsync_ARefundTheProviderSettledLater_CompletesItOnce()
    {
        _payment.MarkSucceeded("pi_1", _payment.Amount, Now).IsSuccess.ShouldBeTrue();
        _payment.RefundInFull(Now).IsSuccess.ShouldBeTrue();
        _payments.GetAsync(_payment.Id, Arg.Any<CancellationToken>()).Returns(_payment);

        GivenTheProviderSends(new RefundSettled("evt_1", _payment.Id, "re_1", Succeeded: true));

        (await _service.HandleEventAsync("{}", "signature", Token)).IsSuccess.ShouldBeTrue();

        _payment.Status.ShouldBe(PaymentStatus.Refunded);
    }

    private void GivenThePaymentIsOverdue()
    {
        _payments.ListOverdueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([_payment]);

        _payments.GetAsync(_payment.Id, Arg.Any<CancellationToken>()).Returns(_payment);
    }

    [Fact]
    public async Task ExpireOverdueAsync_WhenTheGuestPaidAtTheLastSecond_ConfirmsTheBookingInsteadOfReleasingIt()
    {
        GivenThePaymentIsOverdue();

        _provider.ExpireCheckoutAsync(CheckoutId, Arg.Any<CancellationToken>())
            .Returns(new CheckoutAlreadyPaid("pi_1", _payment.Amount));

        await _service.ExpireOverdueAsync(50, Token);

        _payment.Status.ShouldBe(PaymentStatus.Succeeded);
        _booking.Status.ShouldBe(BookingStatus.Confirmed);
        _booking.Nights.ShouldNotBeEmpty("the guest paid for these nights");
    }

    [Fact]
    public async Task ExpireOverdueAsync_WhenTheProviderCannotBeAsked_KeepsTheNightsHeldForTheNextRun()
    {
        GivenThePaymentIsOverdue();

        _provider.ExpireCheckoutAsync(CheckoutId, Arg.Any<CancellationToken>())
            .Returns<Result<CheckoutExpiry>>(_ => throw new HttpRequestException("provider down"));

        await _service.ExpireOverdueAsync(50, Token);

        _payment.Status.ShouldBe(PaymentStatus.Pending, "releasing without asking could drop a payment already made");
        _booking.Nights.ShouldNotBeEmpty();
        _unitOfWork.Saves.ShouldBe(0);
    }

    private sealed class InlineUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }

        public Task<Result<Success>> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saves++;

            return Task.FromResult<Result<Success>>(Result.Success);
        }

        public Task<Result<TValue>> ExecuteInTransactionAsync<TValue>(
            Func<CancellationToken, Task<Result<TValue>>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }
}
