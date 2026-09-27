using Microsoft.AspNetCore.Mvc;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.Execution;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Project execution: WBS/schedule, resources, issues, dependencies, change control, health, closure and benefits (SRS §5.8).</summary>
[Route("api/v1")]
[HasPermission(Permissions.PortfolioRead)]
public sealed class ExecutionController : TetaControllerBase
{
    private readonly IExecutionService _execution;

    public ExecutionController(IExecutionService execution) => _execution = execution;

    [HttpGet("projects/{projectId:guid}/wbs")]
    public Task<IReadOnlyList<WbsDto>> Wbs(Guid projectId, CancellationToken ct) => _execution.GetWbsAsync(projectId, ct);

    [HttpPost("projects/{projectId:guid}/wbs"), HasPermission(Permissions.ExecutionManage)]
    public Task<WbsDto> AddWbs(Guid projectId, SaveWbsRequest request, CancellationToken ct) => _execution.SaveWbsAsync(projectId, null, request, ct);

    [HttpPut("projects/{projectId:guid}/wbs/{id:guid}"), HasPermission(Permissions.ExecutionManage)]
    public Task<WbsDto> UpdateWbs(Guid projectId, Guid id, SaveWbsRequest request, CancellationToken ct) => _execution.SaveWbsAsync(projectId, id, request, ct);

    [HttpPost("projects/{projectId:guid}/wbs/{id:guid}/progress"), HasPermission(Permissions.ExecutionManage)]
    public Task<WbsDto> Progress(Guid projectId, Guid id, ProgressRequest request, CancellationToken ct) => _execution.RecordProgressAsync(projectId, id, request, ct);

    [HttpGet("projects/{projectId:guid}/wbs/{id:guid}/progress")]
    public Task<IReadOnlyList<ProgressDto>> ProgressHistory(Guid projectId, Guid id, CancellationToken ct) => _execution.GetProgressHistoryAsync(projectId, id, ct);

    [HttpPost("projects/{projectId:guid}/dependencies"), HasPermission(Permissions.ExecutionManage)]
    public Task<DependencyDto> AddDependency(Guid projectId, SaveDependencyRequest request, CancellationToken ct) => _execution.AddDependencyAsync(projectId, request, ct);

    [HttpDelete("projects/{projectId:guid}/dependencies/{dependencyId:guid}"), HasPermission(Permissions.ExecutionManage)]
    public async Task<IActionResult> RemoveDependency(Guid projectId, Guid dependencyId, CancellationToken ct)
    {
        await _execution.RemoveDependencyAsync(projectId, dependencyId, ct);
        return NoContent();
    }

    [HttpPost("projects/{projectId:guid}/baseline"), HasPermission(Permissions.ProjectManage, Permissions.ProjectGate)]
    public Task<BaselineDto> Baseline(Guid projectId, ReasonRequest request, CancellationToken ct) => _execution.ApproveBaselineAsync(projectId, request.Reason, ct);

    [HttpGet("projects/{projectId:guid}/gantt")]
    public Task<GanttDto> Gantt(Guid projectId, CancellationToken ct) => _execution.GetGanttAsync(projectId, ct);

    [HttpGet("projects/{projectId:guid}/resources")]
    public Task<IReadOnlyList<ResourceDto>> Resources(Guid projectId, CancellationToken ct) => _execution.ListResourcesAsync(projectId, ct);

    [HttpPost("projects/{projectId:guid}/resources"), HasPermission(Permissions.ExecutionManage)]
    public Task<ResourceDto> AddResource(Guid projectId, SaveResourceRequest request, CancellationToken ct) => _execution.SaveResourceAsync(projectId, null, request, ct);

    [HttpPut("projects/{projectId:guid}/resources/{id:guid}"), HasPermission(Permissions.ExecutionManage)]
    public Task<ResourceDto> UpdateResource(Guid projectId, Guid id, SaveResourceRequest request, CancellationToken ct) => _execution.SaveResourceAsync(projectId, id, request, ct);

    [HttpGet("resources/workload")]
    public Task<IReadOnlyList<WorkloadDto>> Workload(CancellationToken ct) => _execution.WorkloadAsync(ct);

    [HttpGet("issues")]
    public Task<IReadOnlyList<IssueDto>> Issues([FromQuery] Guid? projectId, [FromQuery] bool openOnly = true, CancellationToken ct = default) =>
        _execution.ListIssuesAsync(projectId, openOnly, ct);

    [HttpPost("issues"), HasPermission(Permissions.ExecutionManage)]
    public Task<IssueDto> AddIssue(SaveIssueRequest request, CancellationToken ct) => _execution.SaveIssueAsync(null, request, ct);

    [HttpPut("issues/{id:guid}"), HasPermission(Permissions.ExecutionManage)]
    public Task<IssueDto> UpdateIssue(Guid id, SaveIssueRequest request, CancellationToken ct) => _execution.SaveIssueAsync(id, request, ct);

    [HttpGet("projects/{projectId:guid}/dependency-register")]
    public Task<IReadOnlyList<DependencyItemDto>> DependencyRegister(Guid projectId, CancellationToken ct) => _execution.ListDependencyItemsAsync(projectId, ct);

    [HttpPost("projects/{projectId:guid}/dependency-register"), HasPermission(Permissions.ExecutionManage)]
    public Task<DependencyItemDto> AddDependencyItem(Guid projectId, SaveDependencyItemRequest request, CancellationToken ct) =>
        _execution.SaveDependencyItemAsync(projectId, null, request, ct);

    [HttpPut("projects/{projectId:guid}/dependency-register/{id:guid}"), HasPermission(Permissions.ExecutionManage)]
    public Task<DependencyItemDto> UpdateDependencyItem(Guid projectId, Guid id, SaveDependencyItemRequest request, CancellationToken ct) =>
        _execution.SaveDependencyItemAsync(projectId, id, request, ct);

    [HttpGet("change-requests")]
    public Task<IReadOnlyList<ChangeRequestDto>> Changes([FromQuery] Guid? projectId, CancellationToken ct) => _execution.ListChangesAsync(projectId, ct);

    [HttpPost("change-requests"), HasPermission(Permissions.ExecutionManage)]
    public Task<ChangeRequestDto> AddChange(SaveChangeRequest request, CancellationToken ct) => _execution.SaveChangeAsync(null, request, ct);

    [HttpPut("change-requests/{id:guid}"), HasPermission(Permissions.ExecutionManage)]
    public Task<ChangeRequestDto> UpdateChange(Guid id, SaveChangeRequest request, CancellationToken ct) => _execution.SaveChangeAsync(id, request, ct);

    [HttpGet("change-requests/{id:guid}/impact")]
    public Task<ImpactAssessmentDto> Impact(Guid id, CancellationToken ct) => _execution.AssessChangeAsync(id, ct);

    [HttpPost("change-requests/{id:guid}/submit"), HasPermission(Permissions.ExecutionManage)]
    public Task<WorkflowInstanceDto> SubmitChange(Guid id, CancellationToken ct) => _execution.SubmitChangeAsync(id, ct);

    [HttpPost("projects/{projectId:guid}/health"), HasPermission(Permissions.ExecutionManage, Permissions.PortfolioManage)]
    public Task<HealthDto> CalculateHealth(Guid projectId, CancellationToken ct) => _execution.CalculateHealthAsync(projectId, ct);

    [HttpGet("projects/{projectId:guid}/health")]
    public Task<IReadOnlyList<HealthDto>> HealthHistory(Guid projectId, CancellationToken ct) => _execution.HealthHistoryAsync(projectId, ct);

    [HttpGet("comments")]
    public Task<IReadOnlyList<CommentDto>> Comments([FromQuery] string parentType, [FromQuery] Guid parentId, CancellationToken ct) =>
        _execution.ListCommentsAsync(parentType, parentId, ct);

    [HttpPost("comments")]
    public Task<CommentDto> AddComment(AddCommentRequest request, CancellationToken ct) => _execution.AddCommentAsync(request, ct);

    [HttpGet("projects/{projectId:guid}/closure")]
    public Task<ClosureDto> Closure(Guid projectId, CancellationToken ct) => _execution.GetClosureAsync(projectId, ct);

    [HttpPut("projects/{projectId:guid}/closure"), HasPermission(Permissions.ProjectManage)]
    public Task<ClosureDto> SaveClosure(Guid projectId, SaveClosureRequest request, CancellationToken ct) => _execution.SaveClosureAsync(projectId, request, ct);

    [HttpPost("projects/{projectId:guid}/closure/submit"), HasPermission(Permissions.ProjectManage)]
    public Task<WorkflowInstanceDto> SubmitClosure(Guid projectId, CancellationToken ct) => _execution.SubmitClosureAsync(projectId, ct);

    [HttpGet("projects/{projectId:guid}/benefit-reviews")]
    public Task<IReadOnlyList<BenefitReviewDto>> BenefitReviews(Guid projectId, CancellationToken ct) => _execution.ListBenefitReviewsAsync(projectId, ct);

    [HttpPost("projects/{projectId:guid}/benefit-reviews"), HasPermission(Permissions.ProjectManage, Permissions.PortfolioManage)]
    public Task<BenefitReviewDto> AddBenefitReview(Guid projectId, SaveBenefitReviewRequest request, CancellationToken ct) =>
        _execution.SaveBenefitReviewAsync(projectId, null, request, ct);

    [HttpPut("projects/{projectId:guid}/benefit-reviews/{id:guid}"), HasPermission(Permissions.ProjectManage, Permissions.PortfolioManage)]
    public Task<BenefitReviewDto> UpdateBenefitReview(Guid projectId, Guid id, SaveBenefitReviewRequest request, CancellationToken ct) =>
        _execution.SaveBenefitReviewAsync(projectId, id, request, ct);

    [HttpGet("projects/{projectId:guid}/status-report")]
    public Task<StatusReportDto> StatusReport(Guid projectId, CancellationToken ct) => _execution.StatusReportAsync(projectId, ct);
}
