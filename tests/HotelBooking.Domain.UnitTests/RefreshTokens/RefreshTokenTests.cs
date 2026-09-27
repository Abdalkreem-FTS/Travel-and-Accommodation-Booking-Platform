using HotelBooking.Domain.RefreshTokens;

namespace HotelBooking.Domain.UnitTests.RefreshTokens;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private static readonly Guid UserId = new("00000000-0000-0000-000f-000000000001");
    private static readonly Guid TokenId = new("00000000-0000-0000-0001-000000000001");
    private static readonly Guid FamilyId = new("00000000-0000-0000-0001-000000000002");
    private static readonly Guid ReplacementId = new("00000000-0000-0000-0001-000000000003");

    private static RefreshToken Issue(string hash = "hash-1") =>
        RefreshToken.IssueForNewFamily(TokenId, FamilyId, UserId, hash, Now, Lifetime);

    [Fact]
    public void Rotate_WithAnActiveToken_RevokesTheOldOneSoItCanNeverBeUsedAgain()
    {
        var token = Issue();

        var rotated = token.Rotate(ReplacementId, "hash-2", Now.AddMinutes(5), Lifetime);

        rotated.IsSuccess.ShouldBeTrue();
        token.RevokedAtUtc.ShouldBe(Now.AddMinutes(5));
        token.IsActive(Now.AddMinutes(5)).ShouldBeFalse();
    }

    [Fact]
    public void Rotate_AnAlreadyRotatedToken_ReportsReuseBecauseTheTokenHasLeaked()
    {
        var token = Issue();
        token.Rotate(ReplacementId, "hash-2", Now.AddMinutes(5), Lifetime);

        var replayed = token.Rotate(ReplacementId, "hash-3", Now.AddMinutes(6), Lifetime);

        replayed.IsSuccess.ShouldBeFalse();
        replayed.TopError.Code.ShouldBe("Auth.RefreshTokenReused");
    }

    [Fact]
    public void Rotate_AnExpiredToken_ReportsExpiryRatherThanReuseBecauseItIsNotAnAttack()
    {
        var token = Issue();

        var result = token.Rotate(ReplacementId, "hash-2", Now.Add(Lifetime).AddSeconds(1), Lifetime);

        result.IsSuccess.ShouldBeFalse();
        result.TopError.Code.ShouldBe("Auth.RefreshTokenExpired");
    }

}
