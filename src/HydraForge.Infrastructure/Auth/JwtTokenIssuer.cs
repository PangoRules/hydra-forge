using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HydraForge.Application.Auth;
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
    public string IssueToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.Username),
            new Claim("is_admin", user.IsAdmin.ToString().ToLower()),
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(accessTokenMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

