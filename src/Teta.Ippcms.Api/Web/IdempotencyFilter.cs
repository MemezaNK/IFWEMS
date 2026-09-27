using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Domain.Admin;

namespace Teta.Ippcms.Api.Web;

/// <summary>
/// Idempotent POSTs (SRS §8.1): when a client sends <c>Idempotency-Key</c>, the first successful
/// response is stored and replayed for retries with the same key (per user), so network retries
/// never create duplicate transactions.
/// </summary>
public sealed class IdempotencyFilter : IAsyncActionFilter
{
    public const string Header = "Idempotency-Key";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public IdempotencyFilter(ITetaDbContext db, ICurrentUser user, IClock clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!HttpMethods.IsPost(request.Method) || !request.Headers.TryGetValue(Header, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            await next();
            return;
        }

        var supplied = raw.ToString().Trim();
        if (supplied.Length > 100)
        {
            context.Result = new BadRequestObjectResult(new ProblemDetails { Title = "Idempotency-Key must be at most 100 characters.", Status = 400 });
            return;
        }
        var key = $"{_user.UserId}:{supplied}";
        var path = request.Path.Value ?? string.Empty;
        var existing = await _db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(r => r.Key == key, context.HttpContext.RequestAborted);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestPath, path, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new ConflictObjectResult(new ProblemDetails { Title = "This Idempotency-Key was already used for a different request.", Status = 409 });
                return;
            }
            context.HttpContext.Response.Headers["Idempotent-Replay"] = "true";
            context.Result = new ContentResult { StatusCode = existing.StatusCode, Content = existing.ResponseBody, ContentType = "application/json" };
            return;
        }

        var executed = await next();
        if (executed.Exception is null && executed.Result is ObjectResult { StatusCode: null or (>= 200 and < 300) } result)
        {
            _db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Key = key,
                RequestPath = path.Length > 400 ? path[..400] : path,
                UserId = _user.UserId,
                StatusCode = result.StatusCode ?? 200,
                ResponseBody = JsonSerializer.Serialize(result.Value, result.Value?.GetType() ?? typeof(object), Json),
                CreatedAtUtc = _clock.UtcNow
            });
            try
            {
                await _db.SaveChangesAsync(context.HttpContext.RequestAborted);
            }
            catch (DbUpdateException)
            {
                // A concurrent retry stored the same key first; the business operation itself is protected by its own checks.
            }
        }
    }
}
