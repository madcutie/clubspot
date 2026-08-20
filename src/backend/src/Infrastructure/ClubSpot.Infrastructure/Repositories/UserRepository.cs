using ClubSpot.Application.Core.Users;
using ClubSpot.Domain.Core;
using ClubSpot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class UserRepository(ClubSpotDbContext db) : IUserRepository
{
    // The one place in the system that ignores the tenant filter: at sign-in there is no tenant yet.
    // What makes it safe is that users.email is unique across the whole install (ADR-0018).
    public Task<User?> FindForSignInAsync(string email, CancellationToken cancellationToken) =>
        db.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(user => user.Email == email.Trim().ToLowerInvariant(), cancellationToken);
}
