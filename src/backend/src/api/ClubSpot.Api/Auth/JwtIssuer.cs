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
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("tenant", user.TenantId.Value.ToString()),
            new(ClaimTypes.Name, user.Name)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role.ToString())));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials));
    }
}
