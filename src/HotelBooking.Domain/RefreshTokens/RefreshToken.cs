using HotelBooking.Domain.Abstractions;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.RefreshTokens;

public sealed class RefreshToken : AggregateRoot<Guid>
{
    private RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        Guid familyId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    private RefreshToken() => TokenHash = null!;

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public Guid FamilyId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken IssueForNewFamily(
        Guid tokenId,
        Guid familyId,
        Guid userId,
        string tokenHash,
        DateTimeOffset nowUtc,
        TimeSpan lifetime) =>
        new(tokenId, userId, tokenHash, familyId, nowUtc, nowUtc.Add(lifetime));

    public bool IsActive(DateTimeOffset nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    public Result<RefreshToken> Rotate(
        Guid replacementId,
        string newTokenHash,
        DateTimeOffset nowUtc,
        TimeSpan lifetime)
    {
        if (RevokedAtUtc is not null)
        {
            return RefreshTokenErrors.Reused;
        }

        if (ExpiresAtUtc <= nowUtc)
        {
            return RefreshTokenErrors.Expired;
        }

        var replacement = new RefreshToken(
            replacementId,
            UserId,
            newTokenHash,
            FamilyId,
            nowUtc,
            nowUtc.Add(lifetime));

        RevokedAtUtc = nowUtc;
        ReplacedByTokenId = replacement.Id;

        return replacement;
    }

    public void Revoke(DateTimeOffset nowUtc) => RevokedAtUtc ??= nowUtc;
}
