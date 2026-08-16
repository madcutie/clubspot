using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Core;

public sealed class User : ITenantOwned
{
    private readonly List<UserRole> _userRoles = [];

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Email { get; private set; }
    public string Name { get; private set; }
    public string PasswordHash { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<Role> Roles => _userRoles.Select(userRole => userRole.Role).ToArray();
    public IReadOnlyCollection<UserRole> UserRoles => _userRoles;

    public User(
        Guid id,
        TenantId tenantId,
        string email,
        string name,
        string passwordHash,
        IEnumerable<Role> roles,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email cannot be empty.", nameof(email));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("User name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));

        Id = id;
        TenantId = tenantId;
        Email = email.Trim().ToLowerInvariant();
        Name = name.Trim();
        PasswordHash = passwordHash;
        IsActive = true;
        CreatedAt = createdAt;
        _userRoles.AddRange(roles.Distinct().Select(role => new UserRole(role)));
    }

    public void Deactivate() => IsActive = false;

    private User()
    {
        Email = null!;
        Name = null!;
        PasswordHash = null!;
    }
}
