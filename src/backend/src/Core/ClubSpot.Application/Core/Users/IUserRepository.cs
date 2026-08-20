using ClubSpot.Domain.Core;

namespace ClubSpot.Application.Core.Users;

public interface IUserRepository
{
    // Sign-in runs before there is a tenant, so this lookup crosses tenants on purpose (ADR-0018).
    // The user's own TenantId is what puts everything after it back under a tenant.
    Task<User?> FindForSignInAsync(string email, CancellationToken cancellationToken);
}
