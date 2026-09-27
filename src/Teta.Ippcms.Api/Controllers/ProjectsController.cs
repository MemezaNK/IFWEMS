using Microsoft.AspNetCore.Mvc;
using Platform.Core;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.Budget;
using Teta.Ippcms.Application.Projects;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Portfolio / programme / project governance, business case, prioritisation, charter and stage gates (SRS §5.2).</summary>
[Route("api/v1")]
[HasPermission(Permissions.PortfolioRead)]
public sealed class ProjectsController : TetaControllerBase
{
    private readonly IProjectService _projects;
    private readonly IBudgetService _budget;

    public ProjectsController(IProjectService projects, IBudgetService budget)
    {
        _projects = projects;
        _budget = budget;
    }

    [HttpGet("portfolios")]
    public Task<IReadOnlyList<PortfolioDto>> Portfolios(CancellationToken ct) => _projects.ListPortfoliosAsync(ct);

    [HttpPost("portfolios"), HasPermission(Permissions.PortfolioManage)]
    public Task<PortfolioDto> CreatePortfolio(SavePortfolioRequest request, CancellationToken ct) => _projects.SavePortfolioAsync(null, request, ct);

    [HttpPut("portfolios/{id:guid}"), HasPermission(Permissions.PortfolioManage)]
    public Task<PortfolioDto> UpdatePortfolio(Guid id, SavePortfolioRequest request, CancellationToken ct) => _projects.SavePortfolioAsync(id, request, ct);

    [HttpGet("programmes")]
    public Task<IReadOnlyList<ProgrammeDto>> Programmes([FromQuery] Guid? portfolioId, CancellationToken ct) => _projects.ListProgrammesAsync(portfolioId, ct);

    [HttpPost("programmes"), HasPermission(Permissions.PortfolioManage)]
    public Task<ProgrammeDto> CreateProgramme(SaveProgrammeRequest request, CancellationToken ct) => _projects.SaveProgrammeAsync(null, request, ct);

    [HttpPut("programmes/{id:guid}"), HasPermission(Permissions.PortfolioManage)]
    public Task<ProgrammeDto> UpdateProgramme(Guid id, SaveProgrammeRequest request, CancellationToken ct) => _projects.SaveProgrammeAsync(id, request, ct);

    [HttpGet("portfolio/hierarchy")]
    public Task<IReadOnlyList<HierarchyNode>> Hierarchy(CancellationToken ct) => _projects.GetHierarchyAsync(ct);

    [HttpGet("projects")]
    public Task<PagedResult<ProjectListItemDto>> List([FromQuery] ProjectQuery query, CancellationToken ct) => _projects.ListAsync(query, ct);

    [HttpGet("projects/{id:guid}")]
    public Task<ProjectDetailDto> Get(Guid id, CancellationToken ct) => _projects.GetAsync(id, ct);

    [HttpPost("projects"), HasPermission(Permissions.ProjectCreate)]
    public Task<ProjectDetailDto> Register(SaveProjectRequest request, CancellationToken ct) => _projects.RegisterAsync(request, ct);

    [HttpPut("projects/{id:guid}"), HasPermission(Permissions.ProjectManage)]
    public Task<ProjectDetailDto> Update(Guid id, SaveProjectRequest request, [FromQuery] long version, CancellationToken ct) =>
        _projects.UpdateAsync(id, request, version, ct);

    [HttpPost("projects/{id:guid}/status"), HasPermission(Permissions.ProjectManage, Permissions.ProjectApprove)]
    public Task<ProjectDetailDto> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken ct) => _projects.ChangeStatusAsync(id, request, ct);

    [HttpGet("projects/{id:guid}/status-history")]
    public Task<IReadOnlyList<StatusHistoryDto>> StatusHistory(Guid id, CancellationToken ct) => _projects.GetStatusHistoryAsync(id, ct);

    [HttpGet("projects/{id:guid}/business-case")]
    public Task<BusinessCaseDto> BusinessCase(Guid id, CancellationToken ct) => _projects.GetBusinessCaseAsync(id, ct);

    [HttpPut("projects/{id:guid}/business-case"), HasPermission(Permissions.ProjectCreate, Permissions.ProjectManage)]
    public Task<BusinessCaseDto> SaveBusinessCase(Guid id, SaveBusinessCaseRequest request, CancellationToken ct) => _projects.SaveBusinessCaseAsync(id, request, ct);

    [HttpPost("projects/{id:guid}/business-case/submit"), HasPermission(Permissions.ProjectCreate, Permissions.ProjectManage)]
    public Task<WorkflowInstanceDto> SubmitBusinessCase(Guid id, CancellationToken ct) => _projects.SubmitBusinessCaseAsync(id, ct);

    [HttpGet("prioritisation/criteria")]
    public Task<IReadOnlyList<CriterionDto>> Criteria(CancellationToken ct) => _projects.ListCriteriaAsync(ct);

    [HttpGet("projects/{id:guid}/prioritisation")]
    public Task<PrioritisationDto> Prioritisation(Guid id, CancellationToken ct) => _projects.GetPrioritisationAsync(id, ct);

    [HttpPut("projects/{id:guid}/prioritisation"), HasPermission(Permissions.PortfolioManage)]
    public Task<PrioritisationDto> Score(Guid id, IReadOnlyList<ScoreInput> scores, CancellationToken ct) => _projects.ScoreAsync(id, scores, ct);

    [HttpGet("projects/{id:guid}/charter")]
    public Task<CharterDto?> Charter(Guid id, CancellationToken ct) => _projects.GetCharterAsync(id, ct);

    [HttpPut("projects/{id:guid}/charter"), HasPermission(Permissions.ProjectManage)]
    public Task<CharterDto> SaveCharter(Guid id, SaveCharterRequest request, CancellationToken ct) => _projects.SaveCharterAsync(id, request, ct);

    [HttpPost("projects/{id:guid}/charter/approve"), HasPermission(Permissions.ProjectApprove)]
    public Task<CharterDto> ApproveCharter(Guid id, CancellationToken ct) => _projects.ApproveCharterAsync(id, ct);

    [HttpGet("projects/{id:guid}/stakeholders")]
    public Task<IReadOnlyList<StakeholderDto>> Stakeholders(Guid id, CancellationToken ct) => _projects.ListStakeholdersAsync(id, ct);

    [HttpPost("projects/{id:guid}/stakeholders"), HasPermission(Permissions.ProjectManage)]
    public Task<StakeholderDto> AddStakeholder(Guid id, SaveStakeholderRequest request, CancellationToken ct) => _projects.SaveStakeholderAsync(id, null, request, ct);

    [HttpPut("projects/{id:guid}/stakeholders/{stakeholderId:guid}"), HasPermission(Permissions.ProjectManage)]
    public Task<StakeholderDto> UpdateStakeholder(Guid id, Guid stakeholderId, SaveStakeholderRequest request, CancellationToken ct) =>
        _projects.SaveStakeholderAsync(id, stakeholderId, request, ct);

    [HttpGet("projects/{id:guid}/workstreams")]
    public Task<IReadOnlyList<WorkstreamDto>> Workstreams(Guid id, CancellationToken ct) => _projects.ListWorkstreamsAsync(id, ct);

    [HttpPost("projects/{id:guid}/workstreams"), HasPermission(Permissions.ProjectManage)]
    public Task<WorkstreamDto> AddWorkstream(Guid id, SaveWorkstreamRequest request, CancellationToken ct) => _projects.SaveWorkstreamAsync(id, null, request, ct);

    [HttpPut("projects/{id:guid}/workstreams/{workstreamId:guid}"), HasPermission(Permissions.ProjectManage)]
    public Task<WorkstreamDto> UpdateWorkstream(Guid id, Guid workstreamId, SaveWorkstreamRequest request, CancellationToken ct) =>
        _projects.SaveWorkstreamAsync(id, workstreamId, request, ct);

    [HttpGet("projects/{id:guid}/gates")]
    public Task<IReadOnlyList<GateReviewDto>> Gates(Guid id, CancellationToken ct) => _projects.ListGateReviewsAsync(id, ct);

    [HttpPost("projects/{id:guid}/gates/evaluate"), HasPermission(Permissions.ProjectManage, Permissions.ProjectGate)]
    public Task<GateReviewDto> EvaluateGate(Guid id, CancellationToken ct) => _projects.EvaluateGateAsync(id, ct);

    [HttpPost("projects/{id:guid}/gates/{reviewId:guid}/decision"), HasPermission(Permissions.ProjectGate)]
    public Task<GateReviewDto> DecideGate(Guid id, Guid reviewId, DecideGateRequest request, CancellationToken ct) => _projects.DecideGateAsync(id, reviewId, request, ct);

    // ----- Budget (SRS §5.3) -----
    [HttpGet("projects/{id:guid}/budget")]
    public Task<BudgetSummaryDto> Budget(Guid id, CancellationToken ct) => _budget.GetSummaryAsync(id, ct);

    [HttpPost("projects/{id:guid}/budget/lines"), HasPermission(Permissions.BudgetManage)]
    public Task<BudgetLineDto> AddBudgetLine(Guid id, SaveBudgetLineRequest request, CancellationToken ct) => _budget.AddLineAsync(id, request, ct);

    [HttpPut("projects/{id:guid}/budget/lines/{lineId:guid}"), HasPermission(Permissions.BudgetManage)]
    public Task<BudgetLineDto> UpdateBudgetLine(Guid id, Guid lineId, SaveBudgetLineRequest request, CancellationToken ct) => _budget.UpdateLineAsync(id, lineId, request, ct);

    [HttpPost("projects/{id:guid}/budget/baseline"), HasPermission(Permissions.BudgetApprove, Permissions.BudgetManage)]
    public Task<BudgetSummaryDto> BaselineBudget(Guid id, CancellationToken ct) => _budget.BaselineAsync(id, ct);

    [HttpPost("projects/{id:guid}/budget/lines/{lineId:guid}/revise"), HasPermission(Permissions.BudgetApprove, Permissions.BudgetManage)]
    public Task<BudgetLineDto> ReviseBudget(Guid id, Guid lineId, ReviseBudgetRequest request, CancellationToken ct) => _budget.ReviseAsync(id, lineId, request, ct);

    [HttpPost("projects/{id:guid}/budget/lines/{lineId:guid}/forecast"), HasPermission(Permissions.BudgetManage)]
    public Task<BudgetLineDto> ForecastBudget(Guid id, Guid lineId, ForecastBudgetRequest request, CancellationToken ct) => _budget.ForecastAsync(id, lineId, request, ct);

    [HttpGet("projects/{id:guid}/budget/revisions")]
    public Task<IReadOnlyList<BudgetRevisionDto>> BudgetRevisions(Guid id, CancellationToken ct) => _budget.GetRevisionsAsync(id, ct);

    [HttpGet("projects/{id:guid}/budget/availability")]
    public Task<AvailabilityResult> Availability(Guid id, [FromQuery] Guid? budgetLineId, [FromQuery] decimal amount, CancellationToken ct) =>
        _budget.CheckAvailabilityAsync(id, budgetLineId, amount, null, ct);
}
