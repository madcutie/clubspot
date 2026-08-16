using ClubSpot.Application.Core.Users;
using ClubSpot.Domain.Core;
using Microsoft.AspNetCore.Identity;

namespace ClubSpot.Infrastructure.Auth;

internal sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, password) is not PasswordVerificationResult.Failed;
}
