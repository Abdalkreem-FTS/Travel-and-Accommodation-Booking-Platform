using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using HotelBooking.Application.Abstractions;

namespace HotelBooking.Infrastructure.Authentication;

public sealed class RefreshTokenFactory : IRefreshTokenFactory
{
    private const int TokenSizeInBytes = 32;

    public GeneratedRefreshToken Generate()
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSizeInBytes));

        return new GeneratedRefreshToken(raw, Hash(raw));
    }

    public string Hash(string rawValue) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawValue)));
}
