using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Authentication;
using HotelBooking.Application.Authentication.Dtos;
using HotelBooking.Application.Authentication.Validators;
using HotelBooking.Application.Common;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.RefreshTokens;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Users;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace HotelBooking.Application.UnitTests.Authentication;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly AuthenticationOptions Options = new();

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenDenylist _denylist = Substitute.For<ITokenDenylist>();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        var accessTokens = Substitute.For<IAccessTokenProvider>();
        accessTokens.Issue(Arg.Any<User>(), Arg.Any<Guid>())
            .Returns(new AccessToken("access-token", "jti", Now.AddMinutes(15)));

        var refreshTokenFactory = Substitute.For<IRefreshTokenFactory>();
        refreshTokenFactory.Generate().Returns(new GeneratedRefreshToken("raw", "hash"));
        refreshTokenFactory.Hash(Arg.Any<string>()).Returns("hash");

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var guids = Substitute.For<IGuidProvider>();
        guids.NewSortable().Returns(_ => Guid.NewGuid());
        guids.NewOpaque().Returns(_ => Guid.NewGuid());

        _service = new AuthService(
            _users, _refreshTokens, _unitOfWork, _passwordHasher, accessTokens, refreshTokenFactory,
            Options, _denylist, guids, clock,
            new LoginRequestValidator(), new RefreshSessionRequestValidator(),
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task RefreshAsync_WhenTheRotationLosesTheWriteRace_IsReuseAndCostsTheFamily()
    {
        var token = GivenAnActiveRefreshToken();

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(PersistenceErrors.ConcurrencyConflict);

        var result = await _service.RefreshAsync(
            new RefreshSessionRequest("raw"), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();

        result.TopError.Code.ShouldBe("Auth.InvalidRefreshToken");

        await _refreshTokens.Received(1)
            .RevokeFamilyAsync(token.FamilyId, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ForAnUnknownEmail_StillVerifiesAPasswordSoTheTimingDoesNotLeak()
    {
        _users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _service.LoginAsync(
            new LoginRequest("nobody@example.com", "correct-horse-battery"),
            TestContext.Current.CancellationToken);

        result.TopError.Code.ShouldBe("Auth.InvalidCredentials");
        _passwordHasher.Received(1).Verify(null, "correct-horse-battery");
    }

    [Fact]
    public async Task LogoutAsync_WhenTheDenylistIsDown_ReportsUnavailableButStillKilledTheFamily()
    {
        var sessionId = Guid.NewGuid();

        _denylist.DenySessionAsync(sessionId, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("Redis is unreachable."));

        var result = await _service.LogoutAsync(sessionId, TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.TopError.Code.ShouldBe("Auth.LogoutUnavailable");
        result.TopError.Type.ShouldBe(ErrorType.Unavailable);

        await _refreshTokens.Received(1)
            .RevokeFamilyAsync(sessionId, Now, Arg.Any<CancellationToken>());
    }

    private RefreshToken GivenAnActiveRefreshToken()
    {
        var user = User.Register(
            Guid.NewGuid(), Guid.NewGuid(), Email.Create("abdalkreem@example.com").Value,
            "hashed", "Abdalkreem", "Bzoor", Now).Value;

        var token = RefreshToken.IssueForNewFamily(
            Guid.NewGuid(), Guid.NewGuid(), user.Id, "hash", Now, Options.RefreshTokenLifetime);

        _refreshTokens.GetByTokenHashAsync("hash", Arg.Any<CancellationToken>()).Returns(token);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        return token;
    }
}
