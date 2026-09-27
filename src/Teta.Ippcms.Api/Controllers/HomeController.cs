using Microsoft.AspNetCore.Mvc;
using Platform.Core;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.Admin;
using Teta.Ippcms.Application.Home;
using Teta.Ippcms.Application.Search;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Role-specific home page, notifications, global search and the approvals inbox.</summary>
[Route("api/v1")]
public sealed class HomeController : TetaControllerBase
{
    private readonly IHomeService _home;
    private readonly IAdminService _admin;
    private readonly ISearchService _search;
    private readonly IWorkflowService _workflow;

    public HomeController(IHomeService home, IAdminService admin, ISearchService search, IWorkflowService workflow)
    {
        _home = home;
        _admin = admin;
        _search = search;
        _workflow = workflow;
    }

    public sealed record TaskDecisionRequest(TaskDecision Decision, string? Comment);

    [HttpGet("home")]
    public Task<HomeDto> Home(CancellationToken ct) => _home.GetAsync(ct);

    [HttpGet("notifications")]
    public Task<PagedResult<NotificationDto>> Notifications([FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) => _admin.MyNotificationsAsync(unreadOnly, page, pageSize, ct);

    [HttpGet("notifications/unread-count")]
    public async Task<object> UnreadCount(CancellationToken ct) => new { count = await _admin.UnreadCountAsync(ct) };

    [HttpPost("notifications/read")]
    public async Task<IActionResult> MarkRead([FromQuery] Guid? id, CancellationToken ct)
    {
        await _admin.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpGet("search")]
    public Task<SearchResponse> Search([FromQuery] string q, [FromQuery] string? type, [FromQuery] int limit = 20, CancellationToken ct = default) =>
        _search.SearchAsync(q, type, limit, ct);

    [HttpGet("inbox"), HasPermission(Permissions.WorkflowDecide)]
    public Task<IReadOnlyList<InboxItemDto>> Inbox(CancellationToken ct) => _workflow.GetInboxAsync(ct);

    [HttpPost("inbox/{taskId:guid}/decision"), HasPermission(Permissions.WorkflowDecide)]
    public Task<DecisionResult> Decide(Guid taskId, TaskDecisionRequest request, CancellationToken ct) =>
        _workflow.DecideAsync(taskId, request.Decision, request.Comment, ct);

    [HttpGet("workflows/{instanceId:guid}")]
    public Task<WorkflowInstanceDto> Workflow(Guid instanceId, CancellationToken ct) => _workflow.GetAsync(instanceId, ct);

    [HttpGet("workflows")]
    public Task<IReadOnlyList<WorkflowInstanceDto>> WorkflowsForEntity([FromQuery] string entityType, [FromQuery] Guid entityId, CancellationToken ct) =>
        _workflow.GetForEntityAsync(entityType, entityId, ct);
}
