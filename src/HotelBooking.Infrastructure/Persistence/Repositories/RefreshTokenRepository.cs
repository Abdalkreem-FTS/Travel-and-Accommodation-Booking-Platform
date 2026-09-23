using HotelBooking.Domain.RefreshTokens;

using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository(HotelBookingDbContext context) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        context.RefreshTokens.FirstOrDefaultAsync(
            token => token.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshToken refreshToken) => context.RefreshTokens.Add(refreshToken);

    public Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken = default) =>
        context.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAtUtc, revokedAtUtc),
                cancellationToken);

    public async Task<IReadOnlyList<Guid>> RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var active = context.RefreshTokens.Where(
            token => token.UserId == userId && token.RevokedAtUtc == null);

        var sessionIds = await active
            .Select(token => token.FamilyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await active.ExecuteUpdateAsync(
            setters => setters.SetProperty(token => token.RevokedAtUtc, revokedAtUtc),
            cancellationToken);

        return sessionIds;
    }
}
