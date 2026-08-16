using ClubSpot.Domain.Core;

namespace ClubSpot.Application.Core.Users;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}
