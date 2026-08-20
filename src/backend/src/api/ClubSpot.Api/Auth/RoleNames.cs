using ClubSpot.Domain.Core;

namespace ClubSpot.Api.Auth;

public static class RoleNames
{
    // The wire name of a role, in camelCase like every other enum that crosses the boundary. The
    // issuer and the authorization policies both read it from here so the two cannot drift apart.
    public static string Wire(this Role role)
    {
        var name = role.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
