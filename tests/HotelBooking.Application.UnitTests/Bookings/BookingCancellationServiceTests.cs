using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Bookings;
using HotelBooking.Application.Bookings.Dtos;
using HotelBooking.Application.Payments;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Payments;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace HotelBooking.Application.UnitTests.Bookings;

public sealed class BookingCancellationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 17);

    private static readonly DateOnly CheckIn = Today.AddDays(5);

    private static readonly Guid BookingId = new("00000000-0000-0000-0005-000000000001");
    private static readonly Guid EventId = new("00000000-0000-0000-0006-000000000001");
    private static readonly Guid RoomId = new("00000000-0000-0000-0004-000000000001");
    private static readonly Guid HotelId = new("00000000-0000-0000-0003-000000000001");

    private static readonly Guid UserId = new("00000000-0000-0000-0001-000000000001");

    private static readonly Guid OtherUserId = new("00000000-0000-0000-0001-000000000002");

    private const string CheckoutId = "cs_123";

    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IPaymentProvider _provider = Substitute.For<IPaymentProvider>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly BookingCancellationService _service;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public BookingCancellationServiceTests()
    {
        _clock.UtcNow.Returns(Now);

        var guids = Substitute.For<IGuidProvider>();
        guids.NewSortable().Returns(_ => Guid.NewGuid());

        _unitOfWork.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result<BookingCancellationDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
                call.Arg<Func<CancellationToken, Task<Result<BookingCancellationDto>>>>()(Token));

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);

        _service = new BookingCancellationService(
            _bookings, _payments, _provider, _unitOfWork, guids, _clock,
            NullLogger<BookingCancellationService>.Instance);
    }

    private static Booking APendingBooking()
    {
        var room = Room.Create(
            RoomId, HotelId, "1203", RoomType.Luxury,
            Occupancy.Create(2, 1).Value, Money.Create(120.50m, "USD").Value, Now).Value;

        var stay = DateRange.Create(CheckIn, CheckIn.AddDays(3), Today).Value;

        return Booking.Reserve(
            BookingId,
            UserId,
            [new RoomStay(room, stay, Occupancy.Create(2, 1).Value)],
            ConfirmationNumber.From(EventId),
            Now).Value;
    }

    private Booking GivenABooking()
    {
        var booking = APendingBooking();
        booking.Confirm(EventId, Now).IsSuccess.ShouldBeTrue();

        _bookings.GetWithNightsAsync(BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _bookings.GetWithLinesAsync(BookingId, Arg.Any<CancellationToken>()).Returns(booking);

        return booking;
    }

    [Fact]
    public async Task CancelAsync_OnABookingTheGuestOwns_CancelsItAndReleasesItsNights()
    {
        var booking = GivenABooking();

        var result = await _service.CancelAsync(BookingId, UserId, Token);

        result.IsSuccess.ShouldBeTrue();
        booking.Status.ShouldBe(BookingStatus.Cancelled);
        booking.Nights.ShouldBeEmpty("the ledger rows go in the same transaction as the status");

        result.Value.NightsReleased.ShouldBe(3, "the count is taken before the aggregate clears them");

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_OnABookingTheGuestDoesNotOwn_AnswersNotFoundAndLeavesItAlone()
    {
        var booking = GivenABooking();

        var result = await _service.CancelAsync(BookingId, OtherUserId, Token);

        result.TopError.ShouldBe(BookingErrors.NotFound, "a 403 would confirm that booking id belongs to someone");
        booking.Status.ShouldBe(BookingStatus.Confirmed);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Payment GivenABookingWaitingForPayment(out Booking booking)
    {
        booking = APendingBooking();

        var payment = Payment.Start(Guid.NewGuid(), booking, Now).Value;
        payment.AttachCheckout(CheckoutId, "https://checkout.example/pay/cs_123").IsSuccess.ShouldBeTrue();

        _bookings.GetWithNightsAsync(BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _bookings.GetWithLinesAsync(BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _payments.GetForBookingAsync(BookingId, Arg.Any<CancellationToken>()).Returns(payment);

        return payment;
    }

    [Fact]
    public async Task CancelAsync_OnABookingStillWaitingForPayment_ClosesTheCheckoutFirstSoNoOneCanPayForIt()
    {
        var payment = GivenABookingWaitingForPayment(out var booking);

        _provider.ExpireCheckoutAsync(CheckoutId, Arg.Any<CancellationToken>()).Returns(new CheckoutExpiredUnpaid());

        var result = await _service.CancelAsync(BookingId, UserId, Token);

        result.IsSuccess.ShouldBeTrue();
        booking.Status.ShouldBe(BookingStatus.Cancelled);
        payment.Status.ShouldBe(PaymentStatus.Expired);

        Received.InOrder(() =>
        {
            _provider.ExpireCheckoutAsync(CheckoutId, Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task CancelAsync_WhenTheGuestPaidAtTheLastSecond_KeepsTheBookingAndItsNights()
    {
        var payment = GivenABookingWaitingForPayment(out var booking);

        _provider.ExpireCheckoutAsync(CheckoutId, Arg.Any<CancellationToken>())
            .Returns(new CheckoutAlreadyPaid("pi_123", payment.Amount));

        var result = await _service.CancelAsync(BookingId, UserId, Token);

        result.TopError.ShouldBe(BookingErrors.PaymentJustCompleted);
        booking.Status.ShouldBe(BookingStatus.Pending, "the money is in, so the webhook will confirm it");
        booking.Nights.ShouldNotBeEmpty();

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_OnAPaidBooking_AsksForEveryPennyBackInTheSameTransaction()
    {
        var payment = GivenABookingWaitingForPayment(out var booking);
        payment.MarkSucceeded("pi_123", payment.Amount, Now).IsSuccess.ShouldBeTrue();
        booking.Confirm(EventId, Now).IsSuccess.ShouldBeTrue();

        var result = await _service.CancelAsync(BookingId, UserId, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Refund.ShouldNotBeNull().Amount.ShouldBe(payment.Amount.Amount);
        result.Value.Refund.ResolvedAtUtc.ShouldBeNull("the provider is asked after the commit");
        payment.Status.ShouldBe(PaymentStatus.Refunding);

        await _provider.DidNotReceive().ExpireCheckoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_InsideTheCheckInWindow_NeverClosesTheCheckoutOfABookingItWillNotCancel()
    {
        GivenABookingWaitingForPayment(out var booking);

        _clock.UtcNow.Returns(new DateTimeOffset(CheckIn.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        var result = await _service.CancelAsync(BookingId, UserId, Token);

        result.TopError.ShouldBe(BookingErrors.CancellationWindowClosed);
        booking.Status.ShouldBe(BookingStatus.Pending, "the guest can still pay for it");

        await _provider.DidNotReceive().ExpireCheckoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
