using System.Text;

using HotelBooking.Application.Abstractions;
using HotelBooking.Application.Authentication;
using HotelBooking.Application.Common;
using HotelBooking.Domain.Users;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HotelBooking.Infrastructure.Authentication;

public sealed class JwtAccessTokenProvider : IAccessTokenProvider
{
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly JwtOptions _jwt;
    private readonly AuthenticationOptions _authentication;
    private readonly IDateTimeProvider _clock;
    private readonly IGuidProvider _guids;
    private readonly SigningCredentials _signingCredentials;

    public JwtAccessTokenProvider(
        IOptions<JwtOptions> jwt,
        AuthenticationOptions authentication,
        IDateTimeProvider dateTimeProvider,
        IGuidProvider guidProvider)
    {
        _jwt = jwt.Value;
        _authentication = authentication;
        _clock = dateTimeProvider;
        _guids = guidProvider;

        var key = Encoding.UTF8.GetBytes(_jwt.SigningKey);

        if (key.Length < JwtOptions.MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:SigningKey must be at least " +
                $"{JwtOptions.MinimumSigningKeyBytes} bytes for HS256.");
        }

        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
    }

    public AccessToken Issue(User user, Guid sessionId)
    {
        var jti = _guids.NewOpaque().ToString();
        var issuedAt = _clock.UtcNow;
        var expiresAt = issuedAt.Add(_authentication.AccessTokenLifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = jti,
                [JwtRegisteredClaimNames.Email] = user.Email.Value,
                [ClaimNames.Role] = user.Roles.Select(grant => grant.Role.ToString()).ToArray(),
                [ClaimNames.SessionId] = sessionId.ToString()
            }
        };

        return new AccessToken(Handler.CreateToken(descriptor), jti, expiresAt);
    }

}

public static class ClaimNames
{
    public const string Role = "role";

    public const string SessionId = "sid";
}
