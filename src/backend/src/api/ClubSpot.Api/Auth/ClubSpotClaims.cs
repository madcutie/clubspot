namespace ClubSpot.Api.Auth;

// The shape of the token is contract with the frontend, not an implementation detail of the issuer:
// the backoffice reads these claims to draw itself (ADR-0018). Short names, no ClaimTypes URIs, and
// no implicit mapping in either direction — MapInboundClaims is off.
public static class ClubSpotClaims
{
    public const string Subject = "sub";
    public const string Tenant = "tenant";
    public const string Name = "name";
    public const string Role = "role";
}
