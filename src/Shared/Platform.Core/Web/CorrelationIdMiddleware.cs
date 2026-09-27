using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Platform.Core.Web;

/// <summary>
/// Accepts an inbound X-Correlation-ID (or generates one), echoes it on the response, exposes it
/// via <see cref="HttpContext.Items"/> and opens a logging scope so every log line written while
/// handling the request carries it. Required by TETA SRS §8.1 and NFR-012; shared with IFWEMS.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";
    private const int MaxLength = 100;

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var inbound) && IsValid(inbound.ToString())
            ? inbound.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { [ItemKey] = correlationId }))
        {
            await _next(context);
        }
    }

    // Only accept short, printable identifiers from callers so a header can't be used for log injection.
    private static bool IsValid(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaxLength && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.');

    public static string? Get(HttpContext? context) =>
        context?.Items.TryGetValue(ItemKey, out var value) == true ? value as string : null;
}

public static class CorrelationIdExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}
