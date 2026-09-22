using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Bookings;
using HotelBooking.Application.Bookings.Dtos;
using HotelBooking.Application.Bookings.Validators;
using HotelBooking.Application.Payments;
using HotelBooking.Application.UnitTests.Common;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Bookings;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.Deals;
using HotelBooking.Domain.Idempotency;
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
    private static readonly Guid RoomId = new("00000000-0000-0000-0004-000000000001");
    private static readonly Guid OtherRoomId = new("00000000-0000-0000-0004-000000000002");
    private static readonly Guid HotelId = new("00000000-0000-0000-0003-000000000001");

    private const string Key = "b0a4b0d6-6b5a-4f6c-9d1e-8a7b6c5d4e3f";

    private const string Authorization = "auth_b0a4b0d6";

    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly IRoomRepository _rooms = Substitute.For<IRoomRepository>();
    private readonly IDealRepository _deals = Substitute.For<IDealRepository>();
    private readonly IIdempotencyRepository _idempotency = Substitute.For<IIdempotencyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPaymentGateway _payments = Substitute.For<IPaymentGateway>();
    private readonly IBookingCancellationService _voids = Substitute.For<IBookingCancellationService>();
    private readonly FakeCartRepository _carts = new();
    private readonly BookingService _service;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public BookingServiceTests()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var guids = Substitute.For<IGuidProvider>();
        guids.NewSortable().Returns(_ => Guid.NewGuid());
        guids.NewOpaque().Returns(_ => Guid.NewGuid());

        _unitOfWork.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result<BookingDto>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<BookingDto>>>>()(Token));

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);

        _rooms.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => Rooms(call.Arg<IReadOnlyCollection<Guid>>()));

        _bookings.FindSoldRoomsAsync(Arg.Any<IReadOnlyCollection<RoomStay>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _deals.ListLiveForRoomsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => []);

        _payments.AuthorizeAsync(
                Arg.Any<Guid>(), Arg.Any<Money>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new PaymentAuthorization(Authorization, call.Arg<Money>()));

        _payments.CaptureAsync(Arg.Any<PaymentAuthorization>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success);

        _payments.VoidAsync(Arg.Any<PaymentAuthorization>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success);

        _voids.VoidForFailedPaymentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Updated);

        _service = new BookingService(
            _bookings, _rooms, _deals, _carts, _idempotency, _voids, _payments, _unitOfWork, guids,
            clock, new CreateBookingRequestValidator(), NullLogger<BookingService>.Instance);
    }

    private static IReadOnlyList<Room> Rooms(IReadOnlyCollection<Guid> ids) =>
        [.. ids.Where(id => id == RoomId || id == OtherRoomId).Select(id => ARoom(id))];

    private static Room ARoom(Guid? id = null, int adults = 2, int children = 1) =>
        Room.Create(
            id ?? RoomId, HotelId, "1203", RoomType.Luxury,
            Occupancy.Create(adults, children).Value,
            Money.Create(120.50m, "USD").Value, Now).Value;

    private static BookingItemRequest AnItem(
        Guid? roomId = null, int checkInOffset = 1, int nights = 3, int adults = 2, int children = 1) =>
        new(roomId ?? RoomId,
            Today.AddDays(checkInOffset),
            Today.AddDays(checkInOffset + nights),
            adults,
            children);

    private static CreateBookingRequest ARequest(params BookingItemRequest[] items) =>
        new(items.Length > 0 ? items : [AnItem()]);

    private Task<Result<BookingDto>> CreateAsync(
        CreateBookingRequest? request = null, string? key = Key) =>
        _service.CreateAsync(request ?? ARequest(), UserId, key, Token);

    [Fact]
    public async Task CreateAsync_ForAFreeRoom_ClaimsTheKeyAndFlushesItBeforeTheBookingIsEverAdded()
    {
        var result = await CreateAsync();

        result.IsSuccess.ShouldBeTrue();

        Received.InOrder(() =>
        {
            _idempotency.Add(Arg.Any<IdempotencyRecord>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _bookings.FindSoldRoomsAsync(
                Arg.Any<IReadOnlyCollection<RoomStay>>(), Arg.Any<CancellationToken>());
            _bookings.Add(Arg.Any<Booking>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task CreateAsync_WithNoIdempotencyKey_IsRefusedBeforeATransactionIsOpened(string? key)
    {
        var result = await CreateAsync(key: key);

        result.TopError.ShouldBe(IdempotencyErrors.KeyRequired);
        result.TopError.Type.ShouldBe(ErrorType.PreconditionRequired);

        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<Result<BookingDto>>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ForAFreeRoom_AuthorizesTheCardBeforeTheTransactionAndCapturesAfterIt()
    {
        var result = await CreateAsync();

        result.IsSuccess.ShouldBeTrue();

        Received.InOrder(() =>
        {
            _payments.AuthorizeAsync(
                UserId, Arg.Any<Money>(), AReferenceFor(Key), Arg.Any<CancellationToken>());
            _unitOfWork.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result<BookingDto>>>>(),
                Arg.Any<CancellationToken>());
            _payments.CaptureAsync(Arg.Any<PaymentAuthorization>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task CreateAsync_WhenTheCardIsDeclined_IsRefusedBeforeAnythingIsWritten()
    {
        _payments.AuthorizeAsync(
                Arg.Any<Guid>(), Arg.Any<Money>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PaymentErrors.Declined);

        var result = await CreateAsync();

        result.TopError.ShouldBe(PaymentErrors.Declined);
        result.TopError.Type.ShouldBe(ErrorType.PaymentRequired);

        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<Result<BookingDto>>>>(), Arg.Any<CancellationToken>());

        _bookings.DidNotReceive().Add(Arg.Any<Booking>());
    }

    [Fact]
    public async Task CreateAsync_WhenTheRoomsAreGone_ReleasesTheHoldOnTheGuestsMoney()
    {
        _bookings.FindSoldRoomsAsync(Arg.Any<IReadOnlyCollection<RoomStay>>(), Arg.Any<CancellationToken>())
            .Returns([RoomId]);

        var result = await CreateAsync();

        result.TopError.ShouldBe(BookingErrors.RoomUnavailable);

        await _payments.Received(1).VoidAsync(
            Arg.Is<PaymentAuthorization>(hold => hold.Id == Authorization), Arg.Any<CancellationToken>());

        await _payments.DidNotReceive().CaptureAsync(
            Arg.Any<PaymentAuthorization>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenTheCaptureFails_VoidsTheBookingSoItsNightsGoBackOnSale()
    {
        _payments.CaptureAsync(Arg.Any<PaymentAuthorization>(), Arg.Any<CancellationToken>())
            .Returns(PaymentErrors.CaptureFailed);

        var result = await CreateAsync();

        result.TopError.ShouldBe(PaymentErrors.CaptureFailed);
        result.TopError.Type.ShouldBe(ErrorType.BadGateway);

        await _voids.Received(1).VoidForFailedPaymentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        await _payments.Received(1).VoidAsync(
            Arg.Any<PaymentAuthorization>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenTheSameKeyComesBack_AnswersWithTheBookingItAlreadyMade()
    {
        var original = ABooking();

        _idempotency.FindAsync(UserId, Key, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ARecordFor(original.Id));

        _bookings.GetWithLinesAsync(original.Id, Arg.Any<CancellationToken>()).Returns(original);

        var result = await CreateAsync();

        result.IsSuccess.ShouldBeTrue("a retry of a checkout that worked is not a conflict");
        result.Value.Id.ShouldBe(original.Id);
        result.Value.Lines.Count.ShouldBe(original.Lines.Count);

        await _payments.DidNotReceive().AuthorizeAsync(
            Arg.Any<Guid>(), Arg.Any<Money>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        _bookings.DidNotReceive().Add(Arg.Any<Booking>());
    }

    [Fact]
    public async Task CreateAsync_ForACartSpanningTwoHotels_IsRefusedBeforeTheCardIsTouched()
    {
        _rooms.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([ARoom(RoomId), ARoomElsewhere(OtherRoomId)]);

        var result = await CreateAsync(ARequest(AnItem(RoomId), AnItem(OtherRoomId)));

        result.TopError.ShouldBe(BookingErrors.LinesSpanHotels);

        await _payments.DidNotReceive().AuthorizeAsync(
            Arg.Any<Guid>(), Arg.Any<Money>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static Booking ABooking() =>
        Booking.Create(
            Guid.NewGuid(),
            UserId,
            [
                new RoomStay(
                    ARoom(),
                    DateRange.Create(Today.AddDays(1), Today.AddDays(4), Today).Value,
                    Occupancy.Create(2, 1).Value)
            ],
            ConfirmationNumber.From(Guid.NewGuid()),
            Guid.NewGuid(),
            Now).Value;

    private static IdempotencyRecord ARecordFor(Guid bookingId) =>
        IdempotencyRecord.Claim(UserId, Key, "POST /bookings", bookingId, Now);

    private static string AReferenceFor(string key) =>
        Arg.Is<string>(reference => reference.Contains(key, StringComparison.Ordinal));

    private static Room ARoomElsewhere(Guid id) =>
        Room.Create(
            id, Guid.NewGuid(), "0101", RoomType.Luxury,
            Occupancy.Create(2, 1).Value, Money.Create(95m, "USD").Value, Now).Value;
}
