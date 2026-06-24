using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HydraForge.Application.Auth;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(claim, out userId);
    }

    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        return user.TryGetUserId(out var userId)
            ? userId
            : throw new InvalidOperationException("Authenticated user is missing a valid user id claim.");
    }
}
