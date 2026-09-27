using HotelBooking.Application.Abstractions;

using Microsoft.AspNetCore.Identity;

namespace HotelBooking.Infrastructure.Authentication;

public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private static readonly object Subject = new();

    private readonly PasswordHasher<object> _hasher = new();

    private readonly string _decoyHash;

    public IdentityPasswordHasher(IGuidProvider guidProvider)
    {
        _decoyHash = _hasher.HashPassword(Subject, guidProvider.NewOpaque().ToString());
    }

    public string Hash(string password) => _hasher.HashPassword(Subject, password);

    public bool Verify(string? hash, string password) =>
        _hasher.VerifyHashedPassword(Subject, hash ?? _decoyHash, password)
            != PasswordVerificationResult.Failed;
}
