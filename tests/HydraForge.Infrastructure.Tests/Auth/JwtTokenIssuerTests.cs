using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HydraForge.Domain.Constants;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Infrastructure.Auth;
using Xunit;

namespace HydraForge.Infrastructure.Tests.Auth;

public class JwtTokenIssuerTests
{
    private static readonly JwtTokenIssuer Issuer = new(
        "HydraForge", "HydraForge", "super-secret-key-that-is-at-least-32-bytes-long!!", 60);

    [Fact]
    public void IssueToken_AdminUser_IncludesRoleClaim()
    {
        var user = new User { Id = Guid.NewGuid(), Username = "admin", IsAdmin = true };

        var token = Issuer.IssueToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.Value);
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal(Roles.Admin, roleClaim.Value);
    }

    [Fact]
    public void IssueToken_NonAdminUser_NoRoleClaim()
    {
        var user = new User { Id = Guid.NewGuid(), Username = "user", IsAdmin = false };

        var token = Issuer.IssueToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.Value);
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.Null(roleClaim);
    }

    [Fact]
    public void IssueToken_AdminUser_StillEmitsIsAdminClaim()
    {
        var user = new User { Id = Guid.NewGuid(), Username = "admin", IsAdmin = true };

        var token = Issuer.IssueToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.Value);
        var isAdminClaim = jwt.Claims.FirstOrDefault(c => c.Type == "is_admin");
        Assert.NotNull(isAdminClaim);
        Assert.Equal("true", isAdminClaim.Value);
    }
}
