using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Platform.Core;
using Platform.Core.Web;
using Platform.Security.Tokens;

namespace Platform.Security.Web;

/// <summary>
/// <see cref="ICurrentUser"/> backed by the current HTTP request's principal. Permissions are read
/// from "permission" claims, which applications add server-side (claims transformation) from their
/// own role→permission configuration rather than embedding them in the token.
/// </summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Context => _accessor.HttpContext;
    private ClaimsPrincipal? Principal => Context?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub"), out var id) ? id : null;

    public string? Username => Principal?.FindFirstValue(ClaimTypes.Name);

    public string? DisplayName => Principal?.FindFirstValue(PlatformClaims.DisplayName) ?? Username;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray() ?? Array.Empty<string>();

    public IReadOnlyCollection<string> Permissions =>
        Principal?.FindAll(PlatformClaims.Permission).Select(c => c.Value).Distinct().ToArray() ?? Array.Empty<string>();

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var ua = Context?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrEmpty(ua) ? null : ua.Length > 400 ? ua[..400] : ua;
        }
    }

    public string? CorrelationId => CorrelationIdMiddleware.Get(Context) ?? Context?.TraceIdentifier;

    public string? SessionId => Principal?.FindFirstValue(PlatformClaims.SessionId);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;

    public bool HasPermission(string permission) =>
        Principal?.HasClaim(PlatformClaims.Permission, permission) == true;
}
