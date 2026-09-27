using Microsoft.AspNetCore.Mvc;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.Strategy;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Strategic plan / APP hierarchy, targets, project alignment and performance results (SRS §5.1).</summary>
[Route("api/v1/strategy")]
[HasPermission(Permissions.StrategyRead, Permissions.PerformanceCapture)]
public sealed class StrategyController : TetaControllerBase
{
    private readonly IStrategyService _strategy;

    public StrategyController(IStrategyService strategy) => _strategy = strategy;

    [HttpGet("plans")]
    public Task<IReadOnlyList<PlanSummaryDto>> Plans(CancellationToken ct) => _strategy.ListPlansAsync(ct);

    [HttpGet("plans/{id:guid}")]
    public Task<PlanTreeDto> Plan(Guid id, CancellationToken ct) => _strategy.GetPlanAsync(id, ct);

    [HttpPost("plans"), HasPermission(Permissions.StrategyManage)]
    public Task<PlanSummaryDto> CreatePlan(SavePlanRequest request, CancellationToken ct) => _strategy.CreatePlanAsync(request, ct);

    [HttpPut("plans/{id:guid}"), HasPermission(Permissions.StrategyManage)]
    public Task<PlanSummaryDto> UpdatePlan(Guid id, SavePlanRequest request, [FromQuery] long version, CancellationToken ct) =>
        _strategy.UpdatePlanAsync(id, request, version, ct);

    [HttpPost("plans/{id:guid}/submit"), HasPermission(Permissions.StrategyManage)]
    public Task<WorkflowInstanceDto> SubmitPlan(Guid id, CancellationToken ct) => _strategy.SubmitPlanAsync(id, ct);

    [HttpPost("plans/{id:guid}/revise"), HasPermission(Permissions.StrategyManage)]
    public Task<PlanSummaryDto> RevisePlan(Guid id, ReasonRequest request, CancellationToken ct) => _strategy.RevisePlanAsync(id, request.Reason, ct);

    [HttpGet("plans/{id:guid}/versions")]
    public Task<IReadOnlyList<PlanVersionDto>> Versions(Guid id, CancellationToken ct) => _strategy.GetVersionsAsync(id, ct);

    [HttpPost("plans/{id:guid}/outcomes"), HasPermission(Permissions.StrategyManage)]
    public Task<OutcomeDto> AddOutcome(Guid id, SaveOutcomeRequest request, CancellationToken ct) => _strategy.AddOutcomeAsync(id, request, ct);

    [HttpPost("plans/{id:guid}/objectives"), HasPermission(Permissions.StrategyManage)]
    public Task<ObjectiveDto> AddObjective(Guid id, SaveObjectiveRequest request, CancellationToken ct) => _strategy.AddObjectiveAsync(id, request, ct);

    [HttpPut("objectives/{id:guid}"), HasPermission(Permissions.StrategyManage)]
    public Task<ObjectiveDto> UpdateObjective(Guid id, SaveObjectiveRequest request, CancellationToken ct) => _strategy.UpdateObjectiveAsync(id, request, ct);

    [HttpPost("indicators"), HasPermission(Permissions.StrategyManage)]
    public Task<IndicatorDto> AddIndicator(SaveIndicatorRequest request, CancellationToken ct) => _strategy.AddIndicatorAsync(request, ct);

    [HttpPut("indicators/{id:guid}"), HasPermission(Permissions.StrategyManage)]
    public Task<IndicatorDto> UpdateIndicator(Guid id, SaveIndicatorRequest request, CancellationToken ct) => _strategy.UpdateIndicatorAsync(id, request, ct);

    [HttpPost("targets"), HasPermission(Permissions.StrategyManage)]
    public Task<TargetDto> SaveTarget(SaveTargetRequest request, CancellationToken ct) => _strategy.SaveTargetAsync(request, ct);

    [HttpPost("targets/{id:guid}/submit"), HasPermission(Permissions.StrategyManage)]
    public Task<WorkflowInstanceDto> SubmitTarget(Guid id, CancellationToken ct) => _strategy.SubmitTargetAsync(id, ct);

    [HttpPost("targets/{id:guid}/lock"), HasPermission(Permissions.StrategyApprove, Permissions.StrategyManage)]
    public Task<TargetDto> LockTarget(Guid id, CancellationToken ct) => _strategy.LockTargetAsync(id, ct);

    [HttpPut("targets/{id:guid}/forecast"), HasPermission(Permissions.StrategyManage, Permissions.PerformanceCapture)]
    public Task<TargetDto> Forecast(Guid id, ForecastRequest request, CancellationToken ct) => _strategy.UpdateForecastAsync(id, request, ct);

    [HttpGet("projects/{projectId:guid}/links")]
    public Task<IReadOnlyList<IndicatorLinkDto>> Links(Guid projectId, CancellationToken ct) => _strategy.GetProjectLinksAsync(projectId, ct);

    [HttpPost("projects/{projectId:guid}/links"), HasPermission(Permissions.ProjectManage, Permissions.StrategyManage)]
    public Task<IndicatorLinkDto> Link(Guid projectId, LinkIndicatorRequest request, CancellationToken ct) => _strategy.LinkProjectAsync(projectId, request, ct);

    [HttpDelete("projects/{projectId:guid}/links/{indicatorId:guid}"), HasPermission(Permissions.ProjectManage, Permissions.StrategyManage)]
    public async Task<IActionResult> Unlink(Guid projectId, Guid indicatorId, CancellationToken ct)
    {
        await _strategy.UnlinkProjectAsync(projectId, indicatorId, ct);
        return NoContent();
    }

    [HttpGet("results")]
    public Task<IReadOnlyList<ResultDto>> Results([FromQuery] Guid? indicatorId, [FromQuery] Guid? projectId, [FromQuery] string? financialYear, CancellationToken ct) =>
        _strategy.ListResultsAsync(indicatorId, projectId, financialYear, ct);

    [HttpPost("results"), HasPermission(Permissions.PerformanceCapture)]
    public Task<ResultDto> Capture(CaptureResultRequest request, CancellationToken ct) => _strategy.CaptureResultAsync(request, ct);

    [HttpPost("results/{id:guid}/submit"), HasPermission(Permissions.PerformanceCapture)]
    public Task<ResultDto> SubmitResult(Guid id, CancellationToken ct) => _strategy.SubmitResultAsync(id, ct);

    [HttpPost("results/{id:guid}/verify"), HasPermission(Permissions.PerformanceVerify)]
    public Task<ResultDto> VerifyResult(Guid id, DecisionRequest request, CancellationToken ct) => _strategy.VerifyResultAsync(id, request.Approve, request.Comment, ct);

    [HttpGet("performance")]
    public Task<IReadOnlyList<IndicatorPerformanceDto>> Performance([FromQuery] string financialYear, [FromQuery] int? quarter, [FromQuery] Guid? planId,
        CancellationToken ct) => _strategy.GetPerformanceAsync(financialYear, quarter, planId, ct);
}
