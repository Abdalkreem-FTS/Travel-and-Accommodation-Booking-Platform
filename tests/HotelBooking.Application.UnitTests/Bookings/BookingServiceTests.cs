using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Bookings;
using HotelBooking.Application.Bookings.Dtos;
using HotelBooking.Application.Bookings.Validators;
using HotelBooking.Application.Payments;
using HotelBooking.Application.UnitTests.Common;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Carts;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Idempotency;
using HotelBooking.Domain.Payments;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Rooms;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace HotelBooking.Application.UnitTests.Bookings;

public sealed class BookingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 17);

    private static readonly Guid UserId = new("00000000-0000-0000-0001-000000000001");
    private static readonly Guid OtherUserId = new("00000000-0000-0000-0001-000000000002");
    private static readonly Guid RoomId = new("00000000-0000-0000-0004-000000000001");
    private static readonly Guid OtherRoomId = new("00000000-0000-0000-0004-000000000002");
    private static readonly Guid HotelId = new("00000000-0000-0000-0003-000000000001");

    private const string Key = "b0a4b0d6-6b5a-4f6c-9d1e-8a7b6c5d4e3f";

    private const string CheckoutUrl = "https://checkout.example/pay/cs_123";

    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly IPaymentRepository _payments = Substitute.For<IPaymentRepository>();
    private readonly IRoomRepository _rooms = Substitute.For<IRoomRepository>();
    private readonly IDealRepository _deals = Substitute.For<IDealRepository>();
    private readonly IIdempotencyRepository _idempotency = Substitute.For<IIdempotencyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPaymentProvider _provider = Substitute.For<IPaymentProvider>();
    private readonly FakeCartRepository _carts = new();
    private readonly BookingService _service;

    private Booking? _reserved;
    private Payment? _started;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public BookingServiceTests()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var guids = Substitute.For<IGuidProvider>();
        guids.NewSortable().Returns(_ => Guid.NewGuid());
        guids.NewOpaque().Returns(_ => Guid.NewGuid());

        _unitOfWork.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result<Success>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<Success>>>>()(Token));

        _unitOfWork.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result<Updated>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<Updated>>>>()(Token));

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);

        _rooms.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => Rooms(call.Arg<IReadOnlyCollection<Guid>>()));

        _bookings.FindSoldRoomsAsync(Arg.Any<IReadOnlyCollection<RoomStay>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _bookings.Add(Arg.Do<Booking>(booking => _reserved = booking));
        _bookings.GetWithNightsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => _reserved);

        _payments.Add(Arg.Do<Payment>(payment => _started = payment));
        _payments.GetForBookingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => _started);

        _deals.ListLiveForRoomsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => []);

        _provider.CreateCheckoutAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>())
            .Returns(new ProviderCheckout("cs_123", CheckoutUrl));

        _service = new BookingService(
            _bookings, _payments, _rooms, _deals, _carts, _idempotency, _provider, _unitOfWork, guids,
            clock, new CreateBookingRequestValidator(), NullLogger<BookingService>.Instance);
    }

    private static IReadOnlyList<Room> Rooms(IReadOnlyCollection<Guid> ids) =>
        [.. ids.Select(id => ARoom(id))];

    private static Room ARoom(Guid? id = null) =>
        Room.Create(
            id ?? RoomId, HotelId, "1203", RoomType.Luxury,
            Occupancy.Create(2, 1).Value,
            Money.Create(120.50m, "USD").Value, Now).Value;

    private static DateRange AStay() => DateRange.Create(Today.AddDays(1), Today.AddDays(4), Today).Value;

    private static BookingItemRequest AnItem(Guid? roomId = null) =>
        new(roomId ?? RoomId, Today.AddDays(1), Today.AddDays(4), 2, 1);

    private static CreateBookingRequest ARequest(params BookingItemRequest[] items) =>
        new(items.Length > 0 ? items : [AnItem()]);

    private static CreateBookingRequest TheCart() => new(null);

    private async Task GivenTheStayIsInTheCartAsync()
    {
        var item = CartItem.For(ARoom(), AStay(), Occupancy.Create(2, 1).Value).Value;

        (await _carts.SaveItemAsync(UserId, item, Token)).IsSuccess.ShouldBeTrue();
    }

    private async Task<IReadOnlyList<CartItem>> CartAsync() => (await _carts.GetAsync(UserId, Token)).Value.Items;

    private Task<Result<BookingDto>> CreateAsync(
        CreateBookingRequest? request = null, string? key = Key) =>
        _service.CreateAsync(request ?? ARequest(), UserId, key, Token);

    [Fact]
    public async Task CreateAsync_ReservesTheNightsInOneTransactionAndOpensTheCheckoutOnlyAfterItCommits()
    {
        await GivenTheStayIsInTheCartAsync();

        var result = await CreateAsync(TheCart());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(nameof(BookingStatus.Pending));
        result.Value.Payment.ShouldNotBeNull().CheckoutUrl.ShouldBe(CheckoutUrl);

        Received.InOrder(() =>
        {
            _idempotency.Add(Arg.Any<IdempotencyRecord>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _bookings.FindSoldRoomsAsync(Arg.Any<IReadOnlyCollection<RoomStay>>(), Arg.Any<CancellationToken>());
            _bookings.Add(Arg.Any<Booking>());
            _payments.Add(Arg.Any<Payment>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _provider.CreateCheckoutAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });

        (await CartAsync()).ShouldBeEmpty("the stays are held from the moment they are reserved");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task CreateAsync_WithNoIdempotencyKey_IsRefusedBeforeATransactionIsOpened(string? key)
    {
        var result = await CreateAsync(key: key);

        result.TopError.ShouldBe(IdempotencyErrors.KeyRequired);

        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<Result<Success>>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenNoCheckoutCanBeOpened_ExpiresTheBookingSoItsNightsGoBackOnSale()
    {
        await GivenTheStayIsInTheCartAsync();

        _provider.CreateCheckoutAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>())
            .Returns<Result<ProviderCheckout>>(_ => throw new HttpRequestException("provider down"));

        var result = await CreateAsync(TheCart());

        result.TopError.ShouldBe(PaymentErrors.ProviderUnavailable);

        _reserved.ShouldNotBeNull().Status.ShouldBe(BookingStatus.Expired);
        _reserved.Nights.ShouldBeEmpty();
        _started.ShouldNotBeNull().Status.ShouldBe(PaymentStatus.Expired);

        (await CartAsync()).Count.ShouldBe(1, "nothing was booked, so the cart must still hold the stay");
    }

    [Fact]
    public async Task CreateAsync_WhenTheRoomsAreGone_IsRefusedWithoutEverOpeningACheckout()
    {
        _bookings.FindSoldRoomsAsync(Arg.Any<IReadOnlyCollection<RoomStay>>(), Arg.Any<CancellationToken>())
            .Returns([RoomId]);

        var result = await CreateAsync();

        result.TopError.ShouldBe(BookingErrors.RoomUnavailable);

        await _provider.DidNotReceive().CreateCheckoutAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenTheSameKeyComesBack_AnswersWithTheBookingAndPaymentItAlreadyMade()
    {
        var original = Booking.Reserve(
            Guid.NewGuid(),
            UserId,
            [new RoomStay(ARoom(), AStay(), Occupancy.Create(2, 1).Value)],
            ConfirmationNumber.From(Guid.NewGuid()),
            Now).Value;

        var payment = Payment.Start(Guid.NewGuid(), original, Now).Value;

        _idempotency.FindAsync(UserId, Key, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(IdempotencyRecord.Claim(UserId, Key, "POST /bookings", original.Id, Now));

        _bookings.GetWithLinesAsync(original.Id, Arg.Any<CancellationToken>()).Returns(original);
        _payments.GetForBookingAsync(original.Id, Arg.Any<CancellationToken>()).Returns(payment);

        var result = await CreateAsync();

        result.IsSuccess.ShouldBeTrue("a retry of a checkout that worked is not a conflict");
        result.Value.Id.ShouldBe(original.Id);
        result.Value.Payment.ShouldNotBeNull().Id.ShouldBe(payment.Id);

        await _provider.DidNotReceive().CreateCheckoutAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());

        _bookings.DidNotReceive().Add(Arg.Any<Booking>());
    }

    [Fact]
    public async Task CreateAsync_ForStaysSpanningTwoHotels_IsRefusedBeforeAnythingIsWritten()
    {
        _rooms.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([ARoom(RoomId), ARoomElsewhere(OtherRoomId)]);

        var result = await CreateAsync(ARequest(AnItem(RoomId), AnItem(OtherRoomId)));

        result.TopError.ShouldBe(BookingErrors.LinesSpanHotels);

        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<Result<Success>>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_ForAnotherGuestsBooking_AnswersNotFound()
    {
        var booking = Booking.Reserve(
            Guid.NewGuid(),
            UserId,
            [new RoomStay(ARoom(), AStay(), Occupancy.Create(2, 1).Value)],
            ConfirmationNumber.From(Guid.NewGuid()),
            Now).Value;

        _bookings.GetWithLinesAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var result = await _service.GetAsync(booking.Id, OtherUserId, Token);

        result.TopError.ShouldBe(BookingErrors.NotFound, "a 403 would confirm that booking id belongs to someone");
    }

    private static Room ARoomElsewhere(Guid id) =>
        Room.Create(
            id, Guid.NewGuid(), "0101", RoomType.Luxury,
            Occupancy.Create(2, 1).Value, Money.Create(95m, "USD").Value, Now).Value;
}
