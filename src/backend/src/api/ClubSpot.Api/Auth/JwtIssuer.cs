using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClubSpot.Domain.Core;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClubSpot.Api.Auth;

public sealed class JwtIssuer(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public string Issue(User user)
    {
        var claims = new List<Claim>
        {
            new(ClubSpotClaims.Subject, user.Id.ToString()),
            new(ClubSpotClaims.Tenant, user.TenantId.Value.ToString()),
            new(ClubSpotClaims.Name, user.Name)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClubSpotClaims.Role, role.Wire())));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var handler = new JwtSecurityTokenHandler();
        // Without this the handler rewrites well-known claim types on the way out.
        handler.OutboundClaimTypeMap.Clear();

        return handler.WriteToken(new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials));
    }
}
