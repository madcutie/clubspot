using ClubSpot.Application.Core.Users;
using ClubSpot.Domain.Core;
using ClubSpot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClubSpot.Infrastructure.Repositories;

internal sealed class UserRepository(ClubSpotDbContext db) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(user => user.Email == email.Trim().ToLowerInvariant(), cancellationToken);
}
