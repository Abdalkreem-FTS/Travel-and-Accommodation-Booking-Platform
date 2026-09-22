using System.Security.Claims;
using HotelBooking.Infrastructure.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HotelBooking.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal user)
    {
        public Guid GetUserId()
        {
            var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(subject, out var userId)
                ? userId
                : throw new InvalidOperationException("The authenticated principal carries no usable subject claim.");
        }

        public string? GetJti()
        {
            return user.FindFirstValue(JwtRegisteredClaimNames.Jti);
        }

        public Guid GetSessionId() =>
            user.TryGetSessionId(out var sessionId)
                ? sessionId
                : throw new InvalidOperationException("The authenticated principal carries no usable session claim.");

        public bool TryGetSessionId(out Guid sessionId) =>
            Guid.TryParse(user.FindFirstValue(ClaimNames.SessionId), out sessionId);
    }
}
