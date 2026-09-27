using Microsoft.AspNetCore.Mvc;
using Teta.Ippcms.Api.Web;
using Teta.Ippcms.Application.Reporting;

namespace Teta.Ippcms.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class TetaControllerBase : ControllerBase
{
    protected string? IdempotencyKey =>
        Request.Headers.TryGetValue(IdempotencyFilter.Header, out var v) && !string.IsNullOrWhiteSpace(v) ? v.ToString() : null;

    protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    protected string? UserAgent => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    protected FileContentResult ExportFile(ExportedFile file) => File(file.Content, file.ContentType, file.FileName);
}

/// <summary>Request body for actions that only carry a reason/comment.</summary>
public sealed record ReasonRequest(string Reason);

public sealed record CommentRequest(string? Comment);

public sealed record DecisionRequest(bool Approve, string? Comment);
