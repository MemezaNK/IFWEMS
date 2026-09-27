using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Platform.Core.Web;

/// <summary>
/// Adds baseline browser security headers to every response (clickjacking, MIME sniffing,
/// referrer leakage). HSTS is applied separately by the host only when HTTPS is in use.
/// </summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(self), geolocation=(self), microphone=()";
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    headers["Cache-Control"] = "no-store";
                }
                return Task.CompletedTask;
            });
            await next();
        });
}
