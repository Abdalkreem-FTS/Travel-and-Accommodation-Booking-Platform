using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Authentication;
using HotelBooking.Application.Users;
using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Common;
using HotelBooking.Domain.RefreshTokens;
using HotelBooking.Domain.Results;
using HotelBooking.Domain.Users;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace HotelBooking.Application.UnitTests.Users;

public sealed class UserRoleServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly AuthenticationOptions Options = new();

    private static readonly Guid AdminId = new("00000000-0000-0000-0001-000000000001");
    private static readonly Guid UserId = new("00000000-0000-0000-0001-000000000002");
    private static readonly Guid FirstSession = new("00000000-0000-0000-0007-000000000001");
    private static readonly Guid SecondSession = new("00000000-0000-0000-0007-000000000002");

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ITokenDenylist _denylist = Substitute.For<ITokenDenylist>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly User _omar;
    private readonly UserRoleService _service;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public UserRoleServiceTests()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        _omar = User.Register(
            UserId, Guid.NewGuid(), Email.Create("omar@example.com").Value, "hash", "Omar", "Khaled", Now).Value;
        _omar.Grant(UserRole.Admin, Now);

        _users.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(_omar);
        _refreshTokens.RevokeAllForUserAsync(UserId, Now, Arg.Any<CancellationToken>())
            .Returns([FirstSession, SecondSession]);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);

        _service = new UserRoleService(
            _users, _refreshTokens, _denylist, _unitOfWork, clock, Options, NullLogger<UserRoleService>.Instance);
    }

    [Fact]
    public async Task RevokeAsync_OnYourOwnAccount_IsForbiddenSoTheLastAdminCannotLockEveryoneOut()
    {
        var result = await _service.RevokeAsync(AdminId, "Admin", AdminId, Token);

        result.TopError.ShouldBe(UserErrors.CannotChangeYourOwnRoles);
        await _users.DidNotReceiveWithAnyArgs().GetByIdAsync(Guid.Empty, Token);
    }

    [Fact]
    public async Task RevokeAsync_EndsEverySessionAndDeniesTheAccessTokensStillInFlight()
    {
        var result = await _service.RevokeAsync(UserId, "admin", AdminId, Token);

        result.IsSuccess.ShouldBeTrue();
        _omar.HasRole(UserRole.Admin).ShouldBeFalse();
        await _denylist.Received(1).DenySessionAsync(FirstSession, Options.SessionRevocationWindow, Arg.Any<CancellationToken>());
        await _denylist.Received(1).DenySessionAsync(SecondSession, Options.SessionRevocationWindow, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_WhenTheDenylistIsDown_KeepsTheRevocationButReportsItIncomplete()
    {
        _denylist.DenySessionAsync(Arg.Any<Guid>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException());

        var result = await _service.RevokeAsync(UserId, "Admin", AdminId, Token);

        result.TopError.ShouldBe(UserErrors.RoleRevocationIncomplete);
        _omar.HasRole(UserRole.Admin).ShouldBeFalse();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("")]
    public async Task GrantAsync_ForARoleThatDoesNotExist_IsRefusedBeforeTheUserIsLoaded(string role)
    {
        var result = await _service.GrantAsync(UserId, role, Token);

        result.TopError.ShouldBe(UserErrors.UnknownRole);
        await _users.DidNotReceiveWithAnyArgs().GetByIdAsync(Guid.Empty, Token);
    }
}
