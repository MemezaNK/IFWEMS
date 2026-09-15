using System.Security.Claims;
using IFWEMS.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace IFWEMS.Infrastructure.Auth;

public class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;

    public Guid? UserId
    {
        get
        {
            var value = Context?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Username => Context?.User?.FindFirstValue(ClaimTypes.Name);

    public string? SessionId => Context?.TraceIdentifier;

    public string? IpAddress => Context?.Connection?.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request?.Headers["User-Agent"].ToString();
}
