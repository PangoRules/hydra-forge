using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HydraForge.Application.Auth;
using HydraForge.Domain.Constants;
using HydraForge.Domain.Entities.Auth;
using Microsoft.IdentityModel.Tokens;

namespace HydraForge.Infrastructure.Auth;

public class JwtTokenIssuer(
    string issuer,
    string audience,
    string signingKey,
    int accessTokenMinutes
) : IAccessTokenIssuer
{
    public AccessToken IssueToken(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(accessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.Username),
            new("is_admin", user.IsAdmin.ToString().ToLower()),
        };

        if (user.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, Roles.Admin));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials
        );

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
