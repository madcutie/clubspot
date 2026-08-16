namespace ClubSpot.Domain.Core;

public sealed class UserRole
{
    public Role Role { get; private set; }

    public UserRole(Role role) => Role = role;

    private UserRole()
    {
    }
}
