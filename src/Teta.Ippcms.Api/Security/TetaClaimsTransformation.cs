using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Platform.Security.Tokens;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Security;

namespace Teta.Ippcms.Api.Security;

/// <summary>
/// Replaces the token's role claims with the user's <em>current</em> effective TETA roles and adds
/// the corresponding permission claims (SEC-003/004). Role changes and revocations therefore take
/// effect within a minute without waiting for the token to expire.
/// </summary>
public sealed class TetaClaimsTransformation : IClaimsTransformation
{
    private readonly IUserRoles _roles;
    private readonly IAuthService _auth;
    private readonly IMemoryCache _cache;

    public TetaClaimsTransformation(IUserRoles roles, IAuthService auth, IMemoryCache cache)
    {
        _roles = roles;
        _auth = auth;
        _cache = cache;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true || principal.HasClaim(c => c.Type == "teta_transformed")) return principal;
        if (principal.Identity.AuthenticationType == ErpApiKeyAuthentication.Scheme) return principal;
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return principal;

        var (roles, permissions) = await _cache.GetOrCreateAsync($"teta.claims.{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            var effective = await _roles.GetEffectiveRolesAsync(userId);
            var perms = await _auth.PermissionsForRolesAsync(effective.ToList(), CancellationToken.None);
            return (effective, perms);
        });

        var source = (ClaimsIdentity)principal.Identity;
        var identity = new ClaimsIdentity(source.Claims.Where(c => c.Type != ClaimTypes.Role && c.Type != PlatformClaims.Permission),
            source.AuthenticationType, source.NameClaimType, source.RoleClaimType);
        identity.AddClaims(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        identity.AddClaims(permissions.Select(p => new Claim(PlatformClaims.Permission, p)));
        identity.AddClaim(new Claim("teta_transformed", "1"));
        return new ClaimsPrincipal(identity);
    }

    public static void Invalidate(IMemoryCache cache, Guid userId) => cache.Remove($"teta.claims.{userId}");
}
