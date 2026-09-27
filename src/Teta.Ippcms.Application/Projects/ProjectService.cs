using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Execution;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Monitoring;
using Teta.Ippcms.Domain.Projects;
using Teta.Ippcms.Domain.Scm;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.Application.Projects;

// ---------- Contracts ----------
public sealed record PortfolioDto(Guid Id, string Code, string Name, string? Description, string? OwnerName, int ProgrammeCount, long Version);
public sealed record ProgrammeDto(Guid Id, Guid PortfolioId, string PortfolioName, string Code, string Name, string? Description,
    Guid? OwnerUserId, string? OwnerName, string Status, int ProjectCount, long Version);
public sealed record SavePortfolioRequest(string Code, string Name, string? Description, string? OwnerName);
public sealed record SaveProgrammeRequest(Guid PortfolioId, string Code, string Name, string? Description, Guid? OwnerUserId, string? OwnerName);

public sealed record ProjectListItemDto(Guid Id, string Reference, string? ProjectNumber, string Name, Guid ProgrammeId, string ProgrammeName,
    string PortfolioName, string Status, string Stage, string Health, string? ManagerName, string? SponsorName, DateOnly? PlannedStart,
    DateOnly? PlannedEnd, decimal ApprovedBudget, string? Province, string? OrgUnit, string? ProjectType, decimal? PriorityScore);

public sealed record ProjectQuery(string? Search, Guid? ProgrammeId, Guid? PortfolioId, string? Status, string? Stage, string? Health,
    string? OrgUnit, string? Province, int Page = 1, int PageSize = 50);

public sealed record ProjectDetailDto(
    Guid Id, string Reference, string? ProjectNumber, string DraftReference, string Name, string? Description,
    Guid ProgrammeId, string ProgrammeName, Guid PortfolioId, string PortfolioName,
    Guid? SponsorUserId, string? SponsorName, Guid? ManagerUserId, string? ManagerName, string? BusinessOwner,
    string? OrgUnit, string? ProjectType, string? Province, string? District, string? Municipality,
    string Status, string Stage, string Health, string? HealthExplanation, DateTime? HealthCalculatedAtUtc,
    DateOnly? PlannedStart, DateOnly? PlannedEnd, DateOnly? ActualStart, DateOnly? ActualEnd,
    decimal ApprovedBudget, decimal? PriorityScore, DateTime? ApprovedAtUtc, string? BusinessCaseStatus,
    int OpenIssues, int OpenRisks, int Contracts, int Procurements, int OverdueMilestones, decimal PercentComplete,
    long Version, DateTime CreatedAtUtc, string? CreatedBy);

public sealed record SaveProjectRequest(string Name, string? Description, Guid ProgrammeId, Guid? SponsorUserId, string? SponsorName,
    Guid? ManagerUserId, string? ManagerName, string? BusinessOwner, string? OrgUnit, string? ProjectType, string? Province,
    string? District, string? Municipality, DateOnly? PlannedStart, DateOnly? PlannedEnd);

public sealed record BusinessCaseDto(Guid Id, Guid ProjectId, int VersionNumber, string? Problem, string? Objectives, string? Options,
    string? Scope, string? Benefits, decimal EstimatedCost, string? Risks, string? DeliveryModel, string? ExpectedBenefitMeasure,
    decimal? ExpectedBenefitValue, string Status, DateTime? SubmittedAtUtc, DateTime? DecidedAtUtc, string? DecisionComment,
    IReadOnlyList<string> MissingFields, long Version);

public sealed record SaveBusinessCaseRequest(string? Problem, string? Objectives, string? Options, string? Scope, string? Benefits,
    decimal EstimatedCost, string? Risks, string? DeliveryModel, string? ExpectedBenefitMeasure, decimal? ExpectedBenefitValue);

public sealed record CriterionDto(Guid Id, string Code, string Name, string Category, decimal Weight, int MaxScore, bool IsActive);
public sealed record ScoreInput(Guid CriterionId, decimal Score, string? Rationale);
public sealed record PriorityScoreDto(Guid CriterionId, string CriterionName, string Category, decimal Weight, int MaxScore,
    decimal? Score, string? Rationale, string? ScoredBy, DateTime? ScoredAtUtc);
public sealed record PrioritisationDto(decimal? TotalScore, IReadOnlyList<PriorityScoreDto> Scores);

public sealed record CharterDto(Guid Id, int VersionNumber, string Purpose, string Scope, string GovernanceStructure, string? KeyMilestones,
    string? Assumptions, string Status, DateTime? ApprovedAtUtc, string? ApprovedBy, long Version);
public sealed record SaveCharterRequest(string Purpose, string Scope, string GovernanceStructure, string? KeyMilestones, string? Assumptions);

public sealed record StakeholderDto(Guid Id, Guid? UserId, string Name, string? Organisation, string Role, string? Responsibility,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsCurrent, long Version);
public sealed record SaveStakeholderRequest(Guid? UserId, string Name, string? Organisation, StakeholderRole Role, string? Responsibility,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed record WorkstreamDto(Guid Id, string Code, string Name, string? Lead);
public sealed record SaveWorkstreamRequest(string Code, string Name, string? Lead);

public sealed record GateCheckDto(Guid CriterionId, string Code, string Description, bool IsMandatory, bool IsMet, bool AutoEvaluated, string? Evidence);
public sealed record GateReviewDto(Guid Id, string FromStage, string ToStage, string Decision, string? Comment, DateTime? DecidedAtUtc,
    string? DecidedBy, bool CanPass, IReadOnlyList<GateCheckDto> Checks);
public sealed record ManualCheckInput(Guid CriterionId, bool IsMet, string? Evidence);
public sealed record DecideGateRequest(bool Pass, string? Comment, IReadOnlyList<ManualCheckInput>? ManualChecks);

public sealed record StatusHistoryDto(string FromStatus, string ToStatus, string? FromStage, string? ToStage, DateTime ChangedAtUtc,
    string? ChangedBy, string? Reason);
public sealed record ChangeStatusRequest(ProjectStatus Status, string Reason);

public sealed record HierarchyNode(Guid Id, string Type, string Code, string Name, string? Status, string? Health, IReadOnlyList<HierarchyNode> Children);

// ---------- Service ----------
public interface IProjectService
{
    Task<IReadOnlyList<PortfolioDto>> ListPortfoliosAsync(CancellationToken ct);
    Task<PortfolioDto> SavePortfolioAsync(Guid? id, SavePortfolioRequest request, CancellationToken ct);
    Task<IReadOnlyList<ProgrammeDto>> ListProgrammesAsync(Guid? portfolioId, CancellationToken ct);
    Task<ProgrammeDto> SaveProgrammeAsync(Guid? id, SaveProgrammeRequest request, CancellationToken ct);
    Task<IReadOnlyList<HierarchyNode>> GetHierarchyAsync(CancellationToken ct);

    Task<PagedResult<ProjectListItemDto>> ListAsync(ProjectQuery query, CancellationToken ct);
    Task<ProjectDetailDto> GetAsync(Guid id, CancellationToken ct);
    Task<ProjectDetailDto> RegisterAsync(SaveProjectRequest request, CancellationToken ct);
    Task<ProjectDetailDto> UpdateAsync(Guid id, SaveProjectRequest request, long version, CancellationToken ct);
    Task<ProjectDetailDto> ChangeStatusAsync(Guid id, ChangeStatusRequest request, CancellationToken ct);
    Task<IReadOnlyList<StatusHistoryDto>> GetStatusHistoryAsync(Guid id, CancellationToken ct);

    Task<BusinessCaseDto> GetBusinessCaseAsync(Guid projectId, CancellationToken ct);
    Task<BusinessCaseDto> SaveBusinessCaseAsync(Guid projectId, SaveBusinessCaseRequest request, CancellationToken ct);
    Task<WorkflowInstanceDto> SubmitBusinessCaseAsync(Guid projectId, CancellationToken ct);

    Task<IReadOnlyList<CriterionDto>> ListCriteriaAsync(CancellationToken ct);
    Task<PrioritisationDto> GetPrioritisationAsync(Guid projectId, CancellationToken ct);
    Task<PrioritisationDto> ScoreAsync(Guid projectId, IReadOnlyList<ScoreInput> scores, CancellationToken ct);

    Task<CharterDto?> GetCharterAsync(Guid projectId, CancellationToken ct);
    Task<CharterDto> SaveCharterAsync(Guid projectId, SaveCharterRequest request, CancellationToken ct);
    Task<CharterDto> ApproveCharterAsync(Guid projectId, CancellationToken ct);

    Task<IReadOnlyList<StakeholderDto>> ListStakeholdersAsync(Guid projectId, CancellationToken ct);
    Task<StakeholderDto> SaveStakeholderAsync(Guid projectId, Guid? id, SaveStakeholderRequest request, CancellationToken ct);

    Task<IReadOnlyList<WorkstreamDto>> ListWorkstreamsAsync(Guid projectId, CancellationToken ct);
    Task<WorkstreamDto> SaveWorkstreamAsync(Guid projectId, Guid? id, SaveWorkstreamRequest request, CancellationToken ct);

    Task<GateReviewDto> EvaluateGateAsync(Guid projectId, CancellationToken ct);
    Task<GateReviewDto> DecideGateAsync(Guid projectId, Guid reviewId, DecideGateRequest request, CancellationToken ct);
    Task<IReadOnlyList<GateReviewDto>> ListGateReviewsAsync(Guid projectId, CancellationToken ct);
}

/// <summary>Portfolio, programme and project initiation (SRS §5.2).</summary>
public sealed class ProjectService : IProjectService
{
    public const string BusinessCaseWorkflow = "BUSINESS_CASE_APPROVAL";

    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAccessScope _scope;
    private readonly INumberGenerator _numbers;
    private readonly IWorkflowService _workflow;
    private readonly IGateChecks _gateChecks;
    private readonly ITransactionLedger _ledger;
    private readonly ISodService _sod;
    private readonly IAuditWriter _audit;

    public ProjectService(ITetaDbContext db, ICurrentUser user, IClock clock, IAccessScope scope, INumberGenerator numbers,
        IWorkflowService workflow, IGateChecks gateChecks, ITransactionLedger ledger, ISodService sod, IAuditWriter audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _scope = scope;
        _numbers = numbers;
        _workflow = workflow;
        _gateChecks = gateChecks;
        _ledger = ledger;
        _sod = sod;
        _audit = audit;
    }

    // ----- Portfolio & programmes -----
    public async Task<IReadOnlyList<PortfolioDto>> ListPortfoliosAsync(CancellationToken ct) =>
        await _db.Portfolios.AsNoTracking().OrderBy(p => p.Code)
            .Select(p => new PortfolioDto(p.Id, p.Code, p.Name, p.Description, p.OwnerName,
                _db.Programmes.Count(pr => pr.PortfolioId == p.Id), p.Version))
            .ToListAsync(ct);

    public async Task<PortfolioDto> SavePortfolioAsync(Guid? id, SavePortfolioRequest r, CancellationToken ct)
    {
        new Validator().Required("code", r.Code, 30).Required("name", r.Name, 250).ThrowIfInvalid();
        Portfolio portfolio;
        if (id is null)
        {
            if (await _db.Portfolios.AnyAsync(p => p.Code == r.Code, ct)) throw new ConflictException($"Portfolio code {r.Code} already exists.");
            portfolio = new Portfolio { Code = r.Code.Trim() };
            _db.Portfolios.Add(portfolio);
        }
        else
        {
            portfolio = await _db.Portfolios.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Portfolio", id);
        }
        portfolio.Name = r.Name.Trim();
        portfolio.Description = r.Description;
        portfolio.OwnerName = r.OwnerName;
        await _db.SaveChangesAsync(ct);
        return (await ListPortfoliosAsync(ct)).Single(p => p.Id == portfolio.Id);
    }

    public async Task<IReadOnlyList<ProgrammeDto>> ListProgrammesAsync(Guid? portfolioId, CancellationToken ct)
    {
        var query = from pr in _db.Programmes.AsNoTracking()
                    join pf in _db.Portfolios.AsNoTracking() on pr.PortfolioId equals pf.Id
                    select new { pr, pf };
        if (portfolioId is { } pid) query = query.Where(x => x.pr.PortfolioId == pid);
        var rows = await query.OrderBy(x => x.pr.Code).ToListAsync(ct);
        var counts = await _db.Projects.AsNoTracking().GroupBy(p => p.ProgrammeId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        return rows.Select(x => new ProgrammeDto(x.pr.Id, x.pr.PortfolioId, x.pf.Name, x.pr.Code, x.pr.Name, x.pr.Description, x.pr.OwnerUserId,
            x.pr.OwnerName, x.pr.Status.ToString(), counts.GetValueOrDefault(x.pr.Id), x.pr.Version)).ToList();
    }

    public async Task<ProgrammeDto> SaveProgrammeAsync(Guid? id, SaveProgrammeRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("portfolioId", r.PortfolioId).Required("code", r.Code, 30).Required("name", r.Name, 250).ThrowIfInvalid();
        if (!await _db.Portfolios.AnyAsync(p => p.Id == r.PortfolioId, ct)) throw new ValidationException("portfolioId", "Portfolio not found.");
        Programme programme;
        if (id is null)
        {
            if (await _db.Programmes.AnyAsync(p => p.Code == r.Code, ct)) throw new ConflictException($"Programme code {r.Code} already exists.");
            programme = new Programme { Code = r.Code.Trim() };
            _db.Programmes.Add(programme);
        }
        else
        {
            programme = await _db.Programmes.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Programme", id);
        }
        programme.PortfolioId = r.PortfolioId;
        programme.Name = r.Name.Trim();
        programme.Description = r.Description;
        programme.OwnerUserId = r.OwnerUserId;
        programme.OwnerName = r.OwnerName;
        await _db.SaveChangesAsync(ct);
        return (await ListProgrammesAsync(null, ct)).Single(p => p.Id == programme.Id);
    }

    public async Task<IReadOnlyList<HierarchyNode>> GetHierarchyAsync(CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var portfolios = await _db.Portfolios.AsNoTracking().OrderBy(p => p.Code).ToListAsync(ct);
        var programmes = await _db.Programmes.AsNoTracking().OrderBy(p => p.Code).ToListAsync(ct);
        var projects = await _db.Projects.AsNoTracking().InScope(scope, p => p.Id).OrderBy(p => p.Name).ToListAsync(ct);
        var projectIds = projects.Select(p => p.Id).ToList();
        var workstreams = await _db.Workstreams.AsNoTracking().Where(w => projectIds.Contains(w.ProjectId)).ToListAsync(ct);

        return portfolios.Select(pf => new HierarchyNode(pf.Id, "Portfolio", pf.Code, pf.Name, null, null,
            programmes.Where(pr => pr.PortfolioId == pf.Id).Select(pr => new HierarchyNode(pr.Id, "Programme", pr.Code, pr.Name, pr.Status.ToString(), null,
                projects.Where(p => p.ProgrammeId == pr.Id).Select(p => new HierarchyNode(p.Id, "Project", p.Reference, p.Name, p.Status.ToString(),
                    p.Health.ToString(),
                    workstreams.Where(w => w.ProjectId == p.Id).Select(w => new HierarchyNode(w.Id, "Workstream", w.Code, w.Name, null, null,
                        Array.Empty<HierarchyNode>())).ToList())).ToList())).ToList())).ToList();
    }

    // ----- Projects -----
    public async Task<PagedResult<ProjectListItemDto>> ListAsync(ProjectQuery q, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var query = from p in _db.Projects.AsNoTracking().InScope(scope, p => p.Id)
                    join pr in _db.Programmes.AsNoTracking() on p.ProgrammeId equals pr.Id
                    join pf in _db.Portfolios.AsNoTracking() on pr.PortfolioId equals pf.Id
                    select new { p, pr, pf };

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(x => x.p.Name.Contains(s) || x.p.DraftReference.Contains(s) || (x.p.ProjectNumber != null && x.p.ProjectNumber.Contains(s)));
        }
        if (q.ProgrammeId is { } prog) query = query.Where(x => x.p.ProgrammeId == prog);
        if (q.PortfolioId is { } port) query = query.Where(x => x.pr.PortfolioId == port);
        if (Enum.TryParse<ProjectStatus>(q.Status, true, out var status)) query = query.Where(x => x.p.Status == status);
        if (Enum.TryParse<ProjectStage>(q.Stage, true, out var stage)) query = query.Where(x => x.p.Stage == stage);
        if (Enum.TryParse<HealthStatus>(q.Health, true, out var health)) query = query.Where(x => x.p.Health == health);
        if (!string.IsNullOrWhiteSpace(q.OrgUnit)) query = query.Where(x => x.p.OrgUnit == q.OrgUnit);
        if (!string.IsNullOrWhiteSpace(q.Province)) query = query.Where(x => x.p.Province == q.Province);

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 500);
        var rows = await query.OrderBy(x => x.p.Name).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var items = rows.Select(x => new ProjectListItemDto(x.p.Id, x.p.Reference, x.p.ProjectNumber, x.p.Name, x.p.ProgrammeId, x.pr.Name, x.pf.Name,
            x.p.Status.ToString(), x.p.Stage.ToString(), x.p.Health.ToString(), x.p.ManagerName, x.p.SponsorName, x.p.PlannedStart, x.p.PlannedEnd,
            x.p.ApprovedBudget, x.p.Province, x.p.OrgUnit, x.p.ProjectType, x.p.PriorityScore)).ToList();
        return new PagedResult<ProjectListItemDto>(items, total, page, size);
    }

    public async Task<ProjectDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(id, ct);
        var p = await _db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Project", id);
        var programme = await _db.Programmes.AsNoTracking().SingleAsync(x => x.Id == p.ProgrammeId, ct);
        var portfolio = await _db.Portfolios.AsNoTracking().SingleAsync(x => x.Id == programme.PortfolioId, ct);
        var bc = await _db.BusinessCases.AsNoTracking().Where(b => b.ProjectId == id).OrderByDescending(b => b.VersionNumber).FirstOrDefaultAsync(ct);
        var today = _clock.Today;

        var openIssues = await _db.Issues.CountAsync(i => i.ProjectId == id && (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress || i.Status == IssueStatus.Escalated), ct);
        var openRisks = await _db.Risks.CountAsync(r => r.ProjectId == id && (r.Status == RiskStatus.Open || r.Status == RiskStatus.Treating), ct);
        var contracts = await _db.Contracts.CountAsync(c => c.ProjectId == id, ct);
        var procurements = await _db.Procurements.CountAsync(c => c.ProjectId == id, ct);
        var wbs = await _db.WbsElements.AsNoTracking().Where(w => w.ProjectId == id).ToListAsync(ct);
        var overdue = wbs.Count(w => w.Type == WbsType.Milestone && w.IsOverdue(today));
        var topLevel = wbs.Where(w => w.ParentId == null).ToList();
        var percent = Engines.ScheduleValidator.RollUp(topLevel.Select(w => (w.PercentComplete, w.Weight)));

        return new ProjectDetailDto(p.Id, p.Reference, p.ProjectNumber, p.DraftReference, p.Name, p.Description, p.ProgrammeId, programme.Name,
            portfolio.Id, portfolio.Name, p.SponsorUserId, p.SponsorName, p.ManagerUserId, p.ManagerName, p.BusinessOwner, p.OrgUnit, p.ProjectType,
            p.Province, p.District, p.Municipality, p.Status.ToString(), p.Stage.ToString(), p.Health.ToString(), p.HealthExplanation,
            p.HealthCalculatedAtUtc, p.PlannedStart, p.PlannedEnd, p.ActualStart, p.ActualEnd, p.ApprovedBudget, p.PriorityScore, p.ApprovedAtUtc,
            bc?.Status.ToString(), openIssues, openRisks, contracts, procurements, overdue, percent, p.Version, p.CreatedAtUtc, p.CreatedBy);
    }

    public async Task<ProjectDetailDto> RegisterAsync(SaveProjectRequest r, CancellationToken ct)
    {
        await ValidateProjectAsync(r, ct);
        var project = new Project { DraftReference = await _numbers.NextAsync(NumberPrefixes.ProjectDraft, ct) };
        Apply(project, r);
        if (project.ManagerUserId is null && _user.UserId is { } me)
        {
            project.ManagerUserId = me;
            project.ManagerName ??= _user.DisplayName;
        }
        _db.Projects.Add(project);
        _db.BusinessCases.Add(new BusinessCase { ProjectId = project.Id });
        AddHistory(project, project.Status, project.Status, null, project.Stage, "Concept registered");
        _ledger.Record(nameof(Project), project.Id, "Register", project.Id);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(project.Id, ct);
    }

    public async Task<ProjectDetailDto> UpdateAsync(Guid id, SaveProjectRequest r, long version, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(id, ct);
        await ValidateProjectAsync(r, ct);
        var project = await _db.Projects.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Project", id);
        if (version > 0 && project.Version != version) throw new ConflictException("The project was changed by someone else. Reload and try again.");
        if (project.IsClosedOrCancelled) throw new DomainException($"Project {project.Reference} is {project.Status} and read-only.", "FR-PPM-010");
        if (project.Status == ProjectStatus.SubmittedForApproval)
            throw new DomainException("The project cannot be edited while its business case is under approval.", "FR-PPM-004");
        Apply(project, r);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ProjectDetailDto> ChangeStatusAsync(Guid id, ChangeStatusRequest r, CancellationToken ct)
    {
        new Validator().Required("reason", r.Reason, 2000).ThrowIfInvalid();
        await _scope.EnsureProjectAsync(id, ct);
        var project = await _db.Projects.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Project", id);
        var from = project.Status;
        var allowed = (from, r.Status) switch
        {
            (ProjectStatus.InExecution, ProjectStatus.OnHold) => true,
            (ProjectStatus.Approved, ProjectStatus.OnHold) => true,
            (ProjectStatus.OnHold, ProjectStatus.InExecution) => project.Stage == ProjectStage.Execution,
            (ProjectStatus.OnHold, ProjectStatus.Approved) => project.Stage == ProjectStage.Planning,
            (_, ProjectStatus.Cancelled) => !project.IsClosedOrCancelled,
            _ => false
        };
        if (!allowed)
            throw new DomainException($"A project cannot move from {from} to {r.Status} directly; use the controlled workflow/stage gates.", "FR-PPM-010");

        if (r.Status == ProjectStatus.Cancelled)
        {
            await _workflow.CancelForEntityAsync(nameof(BusinessCase), (await _db.BusinessCases.Where(b => b.ProjectId == id)
                .OrderByDescending(b => b.VersionNumber).Select(b => b.Id).FirstOrDefaultAsync(ct)), r.Reason, ct);
        }
        project.Status = r.Status;
        AddHistory(project, from, r.Status, project.Stage, project.Stage, r.Reason);
        _audit.Write("Projects", nameof(Project), id.ToString(), "StatusChanged", new { From = from.ToString(), To = r.Status.ToString() }, r.Reason);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyList<StatusHistoryDto>> GetStatusHistoryAsync(Guid id, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(id, ct);
        var rows = await _db.ProjectStatusHistory.AsNoTracking().Where(h => h.ProjectId == id).OrderByDescending(h => h.ChangedAtUtc).ToListAsync(ct);
        return rows.Select(h => new StatusHistoryDto(h.FromStatus.ToString(), h.ToStatus.ToString(), h.FromStage?.ToString(), h.ToStage?.ToString(),
            h.ChangedAtUtc, h.ChangedBy, h.Reason)).ToList();
    }

    // ----- Business case -----
    public async Task<BusinessCaseDto> GetBusinessCaseAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var bc = await CurrentBusinessCaseAsync(projectId, ct);
        return ToDto(bc);
    }

    public async Task<BusinessCaseDto> SaveBusinessCaseAsync(Guid projectId, SaveBusinessCaseRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator().NonNegative("estimatedCost", r.EstimatedCost).NonNegative("expectedBenefitValue", r.ExpectedBenefitValue)
            .Optional("problem", r.Problem).Optional("objectives", r.Objectives).Optional("options", r.Options).Optional("scope", r.Scope)
            .Optional("benefits", r.Benefits).Optional("risks", r.Risks).Optional("deliveryModel", r.DeliveryModel, 2000).ThrowIfInvalid();
        var bc = await CurrentBusinessCaseAsync(projectId, ct);
        if (bc.Status is BusinessCaseStatus.Submitted or BusinessCaseStatus.Approved)
            throw new DomainException($"The business case is {bc.Status} and cannot be edited.", "FR-PPM-002");
        bc.Problem = r.Problem;
        bc.Objectives = r.Objectives;
        bc.Options = r.Options;
        bc.Scope = r.Scope;
        bc.Benefits = r.Benefits;
        bc.EstimatedCost = r.EstimatedCost;
        bc.Risks = r.Risks;
        bc.DeliveryModel = r.DeliveryModel;
        bc.ExpectedBenefitMeasure = r.ExpectedBenefitMeasure;
        bc.ExpectedBenefitValue = r.ExpectedBenefitValue;

        var project = await _db.Projects.SingleAsync(p => p.Id == projectId, ct);
        if (project.Status == ProjectStatus.Concept)
        {
            AddHistory(project, project.Status, ProjectStatus.BusinessCase, project.Stage, project.Stage, "Business case drafting started");
            project.Status = ProjectStatus.BusinessCase;
        }
        await _db.SaveChangesAsync(ct);
        return ToDto(bc);
    }

    public async Task<WorkflowInstanceDto> SubmitBusinessCaseAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var project = await _db.Projects.SingleAsync(p => p.Id == projectId, ct);
        var bc = await CurrentBusinessCaseAsync(projectId, ct);
        if (bc.Status is not (BusinessCaseStatus.Draft or BusinessCaseStatus.Returned))
            throw new DomainException($"The business case is {bc.Status}; only draft or returned cases can be submitted.", "FR-PPM-004");

        var missing = bc.MissingMandatoryFields();
        if (missing.Count > 0)
            throw new ValidationException(missing.ToDictionary(m => char.ToLowerInvariant(m[0]) + m[1..], _ => new[] { "Required before submission." }));
        if (!await _db.ProjectIndicatorLinks.AnyAsync(l => l.ProjectId == projectId, ct))
            throw new DomainException("The project must be aligned to at least one APP indicator before it can proceed to approval.", "FR-STR-004");
        if (project.PlannedStart is null || project.PlannedEnd is null)
            throw new ValidationException("plannedStart", "Planned start and end dates are required before submission.");

        bc.Status = BusinessCaseStatus.Submitted;
        bc.SubmittedAtUtc = _clock.UtcNow;
        AddHistory(project, project.Status, ProjectStatus.SubmittedForApproval, project.Stage, project.Stage, "Business case submitted");
        project.Status = ProjectStatus.SubmittedForApproval;

        var instance = await _workflow.StartAsync(new StartWorkflowRequest(BusinessCaseWorkflow, nameof(BusinessCase), bc.Id, project.Reference,
            $"Business case: {project.Name}", bc.EstimatedCost, project.Id, $"projects/{project.Id}"), ct);
        await _db.SaveChangesAsync(ct);
        return await _workflow.GetAsync(instance.Id, ct);
    }

    // ----- Prioritisation -----
    public async Task<IReadOnlyList<CriterionDto>> ListCriteriaAsync(CancellationToken ct) =>
        await _db.PrioritisationCriteria.AsNoTracking().OrderBy(c => c.Category).ThenBy(c => c.Code)
            .Select(c => new CriterionDto(c.Id, c.Code, c.Name, c.Category, c.Weight, c.MaxScore, c.IsActive)).ToListAsync(ct);

    public async Task<PrioritisationDto> GetPrioritisationAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var criteria = await _db.PrioritisationCriteria.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Category).ToListAsync(ct);
        var scores = await _db.ProjectPriorityScores.AsNoTracking().Where(s => s.ProjectId == projectId).ToListAsync(ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        return new PrioritisationDto(project.PriorityScore, criteria.Select(c =>
        {
            var s = scores.FirstOrDefault(x => x.CriterionId == c.Id);
            return new PriorityScoreDto(c.Id, c.Name, c.Category, c.Weight, c.MaxScore, s?.Score, s?.Rationale, s?.ScoredBy, s?.ScoredAtUtc);
        }).ToList());
    }

    public async Task<PrioritisationDto> ScoreAsync(Guid projectId, IReadOnlyList<ScoreInput> scores, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var criteria = await _db.PrioritisationCriteria.AsNoTracking().Where(c => c.IsActive).ToListAsync(ct);
        var v = new Validator();
        foreach (var s in scores)
        {
            var c = criteria.FirstOrDefault(x => x.Id == s.CriterionId);
            v.Must(c is not null, $"scores[{s.CriterionId}]", "Unknown criterion.");
            if (c is not null) v.Range($"scores[{c.Code}]", s.Score, 0, c.MaxScore).Required($"rationale[{c.Code}]", s.Rationale, 2000);
        }
        v.ThrowIfInvalid();

        foreach (var s in scores)
        {
            var existing = await _db.ProjectPriorityScores.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.CriterionId == s.CriterionId, ct);
            if (existing is null)
            {
                existing = new ProjectPriorityScore { ProjectId = projectId, CriterionId = s.CriterionId };
                _db.ProjectPriorityScores.Add(existing);
            }
            existing.Score = s.Score;
            existing.Rationale = s.Rationale;
            existing.ScoredAtUtc = _clock.UtcNow;
            existing.ScoredBy = _user.DisplayName ?? _user.Username;
        }

        // Weighted score out of 100 over active criteria that have been scored (FR-PPM-003).
        var project = await _db.Projects.SingleAsync(p => p.Id == projectId, ct);
        var all = _db.ProjectPriorityScores.Local.Where(x => x.ProjectId == projectId).ToList();
        var persisted = await _db.ProjectPriorityScores.AsNoTracking().Where(x => x.ProjectId == projectId).ToListAsync(ct);
        var merged = criteria.Select(c => (c, all.FirstOrDefault(x => x.CriterionId == c.Id) ?? persisted.FirstOrDefault(x => x.CriterionId == c.Id)))
            .Where(x => x.Item2 is not null).ToList();
        var weight = merged.Sum(x => x.c.Weight);
        project.PriorityScore = weight <= 0 ? null : Math.Round(merged.Sum(x => x.Item2!.Score / x.c.MaxScore * x.c.Weight) / weight * 100m, 1);
        await _db.SaveChangesAsync(ct);
        return await GetPrioritisationAsync(projectId, ct);
    }

    // ----- Charter -----
    public async Task<CharterDto?> GetCharterAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var c = await _db.ProjectCharters.AsNoTracking().Where(x => x.ProjectId == projectId).OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        return c is null ? null : ToDto(c);
    }

    public async Task<CharterDto> SaveCharterAsync(Guid projectId, SaveCharterRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator().Required("purpose", r.Purpose).Required("scope", r.Scope).Required("governanceStructure", r.GovernanceStructure).ThrowIfInvalid();
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        if (!project.HasProjectId) throw new DomainException("A charter can only be created once the project has an approved Project ID.", "FR-PPM-007");
        var charter = await _db.ProjectCharters.Where(x => x.ProjectId == projectId).OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        if (charter is null || charter.Status == CharterStatus.Approved)
        {
            charter = new ProjectCharter { ProjectId = projectId, VersionNumber = (charter?.VersionNumber ?? 0) + 1 };
            _db.ProjectCharters.Add(charter);
        }
        charter.Purpose = r.Purpose;
        charter.Scope = r.Scope;
        charter.GovernanceStructure = r.GovernanceStructure;
        charter.KeyMilestones = r.KeyMilestones;
        charter.Assumptions = r.Assumptions;
        _ledger.Record(nameof(ProjectCharter), charter.Id, "Author", projectId);
        await _db.SaveChangesAsync(ct);
        return ToDto(charter);
    }

    public async Task<CharterDto> ApproveCharterAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var charter = await _db.ProjectCharters.Where(x => x.ProjectId == projectId).OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Project charter", projectId);
        if (charter.Status == CharterStatus.Approved) throw new DomainException("The charter is already approved.", "FR-PPM-007");
        var userId = _user.UserId ?? throw new ForbiddenException();
        await _sod.EnsureAllowedAsync(nameof(ProjectCharter), charter.Id, "Approve", userId, ct);
        charter.Status = CharterStatus.Approved;
        charter.ApprovedAtUtc = _clock.UtcNow;
        charter.ApprovedBy = _user.DisplayName ?? _user.Username;
        _ledger.Record(nameof(ProjectCharter), charter.Id, "Approve", projectId);
        await _db.SaveChangesAsync(ct);
        return ToDto(charter);
    }

    // ----- Stakeholders & workstreams -----
    public async Task<IReadOnlyList<StakeholderDto>> ListStakeholdersAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var today = _clock.Today;
        var rows = await _db.ProjectStakeholders.AsNoTracking().Where(s => s.ProjectId == projectId).OrderBy(s => s.Role).ThenBy(s => s.Name).ToListAsync(ct);
        return rows.Select(s => new StakeholderDto(s.Id, s.UserId, s.Name, s.Organisation, s.Role.ToString(), s.Responsibility, s.EffectiveFrom,
            s.EffectiveTo, s.IsEffectiveOn(today), s.Version)).ToList();
    }

    public async Task<StakeholderDto> SaveStakeholderAsync(Guid projectId, Guid? id, SaveStakeholderRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator().Required("name", r.Name, 200).DateOrder("effectiveFrom", r.EffectiveFrom, "effectiveTo", r.EffectiveTo).ThrowIfInvalid();
        ProjectStakeholder s;
        if (id is null)
        {
            s = new ProjectStakeholder { ProjectId = projectId };
            _db.ProjectStakeholders.Add(s);
        }
        else
        {
            s = await _db.ProjectStakeholders.SingleOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId, ct)
                ?? throw new NotFoundException("Stakeholder", id);
        }
        s.UserId = r.UserId;
        s.Name = r.Name.Trim();
        s.Organisation = r.Organisation;
        s.Role = r.Role;
        s.Responsibility = r.Responsibility;
        s.EffectiveFrom = r.EffectiveFrom;
        s.EffectiveTo = r.EffectiveTo;

        // Keep the project master in step with the current sponsor/manager (FR-PPM-008).
        if (s.IsEffectiveOn(_clock.Today) && r.Role is StakeholderRole.Sponsor or StakeholderRole.ProjectManager)
        {
            var project = await _db.Projects.SingleAsync(p => p.Id == projectId, ct);
            if (r.Role == StakeholderRole.Sponsor) { project.SponsorUserId = r.UserId; project.SponsorName = r.Name; }
            else { project.ManagerUserId = r.UserId; project.ManagerName = r.Name; }
        }
        await _db.SaveChangesAsync(ct);
        return new StakeholderDto(s.Id, s.UserId, s.Name, s.Organisation, s.Role.ToString(), s.Responsibility, s.EffectiveFrom, s.EffectiveTo,
            s.IsEffectiveOn(_clock.Today), s.Version);
    }

    public async Task<IReadOnlyList<WorkstreamDto>> ListWorkstreamsAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        return await _db.Workstreams.AsNoTracking().Where(w => w.ProjectId == projectId).OrderBy(w => w.Code)
            .Select(w => new WorkstreamDto(w.Id, w.Code, w.Name, w.Lead)).ToListAsync(ct);
    }

    public async Task<WorkstreamDto> SaveWorkstreamAsync(Guid projectId, Guid? id, SaveWorkstreamRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator().Required("code", r.Code, 30).Required("name", r.Name, 250).ThrowIfInvalid();
        Workstream w;
        if (id is null)
        {
            w = new Workstream { ProjectId = projectId };
            _db.Workstreams.Add(w);
        }
        else
        {
            w = await _db.Workstreams.SingleOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId, ct) ?? throw new NotFoundException("Workstream", id);
        }
        w.Code = r.Code.Trim();
        w.Name = r.Name.Trim();
        w.Lead = r.Lead;
        await _db.SaveChangesAsync(ct);
        return new WorkstreamDto(w.Id, w.Code, w.Name, w.Lead);
    }

    // ----- Stage gates (FR-PPM-009) -----
    public async Task<GateReviewDto> EvaluateGateAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        if (!project.HasProjectId) throw new DomainException("Stage gates apply once the project has an approved Project ID.", "FR-PPM-009");
        var to = NextStage(project.Stage) ?? throw new DomainException("The project has completed all stages.", "FR-PPM-009");

        var pending = await _db.StageGateReviews.Include(r => r.Checks)
            .Where(r => r.ProjectId == projectId && r.Decision == GateDecision.Pending).ToListAsync(ct);
        foreach (var old in pending)
        {
            old.Decision = GateDecision.Failed;
            old.Comment = "Superseded by a new gate evaluation.";
        }

        var criteria = await _db.StageGateCriteria.AsNoTracking().Where(c => c.Stage == project.Stage && c.IsActive).OrderBy(c => c.SortOrder).ToListAsync(ct);
        var review = new StageGateReview { ProjectId = projectId, FromStage = project.Stage, ToStage = to };
        foreach (var c in criteria)
        {
            var (auto, met, evidence) = c.CheckCode is null ? (false, false, null) : await _gateChecks.EvaluateAsync(projectId, c.CheckCode, ct);
            review.Checks.Add(new StageGateCheck
            {
                ReviewId = review.Id, CriterionId = c.Id, CriterionCode = c.Code, CriterionDescription = c.Description, IsMandatory = c.IsMandatory,
                AutoEvaluated = auto, IsMet = met, Evidence = evidence
            });
        }
        _db.StageGateReviews.Add(review);
        await _db.SaveChangesAsync(ct);
        return ToDto(review);
    }

    public async Task<GateReviewDto> DecideGateAsync(Guid projectId, Guid reviewId, DecideGateRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var review = await _db.StageGateReviews.Include(x => x.Checks).SingleOrDefaultAsync(x => x.Id == reviewId && x.ProjectId == projectId, ct)
                     ?? throw new NotFoundException("Gate review", reviewId);
        if (review.Decision != GateDecision.Pending) throw new DomainException("This gate review has already been decided.", "FR-PPM-009");
        var project = await _db.Projects.SingleAsync(p => p.Id == projectId, ct);
        if (project.Stage != review.FromStage) throw new ConflictException("The project stage changed since this gate was evaluated. Re-evaluate the gate.");

        foreach (var manual in r.ManualChecks ?? Array.Empty<ManualCheckInput>())
        {
            var check = review.Checks.FirstOrDefault(c => c.CriterionId == manual.CriterionId);
            if (check is null || check.AutoEvaluated) continue;   // automatic results cannot be overridden
            check.IsMet = manual.IsMet;
            check.Evidence = manual.Evidence;
        }

        var unmet = review.Checks.Where(c => c.IsMandatory && !c.IsMet).ToList();
        if (r.Pass && unmet.Count > 0)
        {
            throw new DomainException(
                $"The gate cannot pass with unmet mandatory criteria: {string.Join("; ", unmet.Select(c => c.CriterionDescription))}.", "FR-PPM-009");
        }

        review.Decision = r.Pass ? GateDecision.Passed : GateDecision.Failed;
        review.Comment = r.Comment;
        review.DecidedAtUtc = _clock.UtcNow;
        review.DecidedBy = _user.DisplayName ?? _user.Username;

        if (r.Pass)
        {
            var fromStatus = project.Status;
            var fromStage = project.Stage;
            project.Stage = review.ToStage;
            project.Status = review.ToStage switch
            {
                ProjectStage.Execution => ProjectStatus.InExecution,
                ProjectStage.CloseOut => ProjectStatus.Closing,
                ProjectStage.BenefitReview or ProjectStage.Completed => ProjectStatus.Closed,
                _ => project.Status
            };
            if (review.ToStage == ProjectStage.Execution && project.ActualStart is null) project.ActualStart = _clock.Today;
            AddHistory(project, fromStatus, project.Status, fromStage, project.Stage, $"Stage gate passed: {fromStage} → {review.ToStage}");
        }
        _audit.Write("Projects", nameof(StageGateReview), review.Id.ToString(), r.Pass ? "GatePassed" : "GateFailed",
            new { From = review.FromStage.ToString(), To = review.ToStage.ToString(), Unmet = unmet.Select(u => u.CriterionCode) }, r.Comment);
        await _db.SaveChangesAsync(ct);
        return ToDto(review);
    }

    public async Task<IReadOnlyList<GateReviewDto>> ListGateReviewsAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var reviews = await _db.StageGateReviews.AsNoTracking().Include(r => r.Checks).Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct);
        return reviews.Select(ToDto).ToList();
    }

    // ----- helpers -----
    internal static ProjectStage? NextStage(ProjectStage stage) => stage switch
    {
        ProjectStage.Initiation => ProjectStage.Planning,
        ProjectStage.Planning => ProjectStage.Execution,
        ProjectStage.Execution => ProjectStage.CloseOut,
        ProjectStage.CloseOut => ProjectStage.BenefitReview,
        ProjectStage.BenefitReview => ProjectStage.Completed,
        _ => null
    };

    private void AddHistory(Project project, ProjectStatus from, ProjectStatus to, ProjectStage? fromStage, ProjectStage? toStage, string reason) =>
        _db.ProjectStatusHistory.Add(new ProjectStatusHistory
        {
            ProjectId = project.Id, FromStatus = from, ToStatus = to, FromStage = fromStage, ToStage = toStage,
            ChangedAtUtc = _clock.UtcNow, ChangedBy = _user.DisplayName ?? _user.Username ?? "system", Reason = reason
        });

    private async Task<BusinessCase> CurrentBusinessCaseAsync(Guid projectId, CancellationToken ct)
    {
        var bc = await _db.BusinessCases.Where(b => b.ProjectId == projectId).OrderByDescending(b => b.VersionNumber).FirstOrDefaultAsync(ct);
        if (bc is null)
        {
            bc = new BusinessCase { ProjectId = projectId };
            _db.BusinessCases.Add(bc);
        }
        return bc;
    }

    private async Task ValidateProjectAsync(SaveProjectRequest r, CancellationToken ct)
    {
        new Validator().Required("name", r.Name, 250).RequiredId("programmeId", r.ProgrammeId).Optional("description", r.Description)
            .DateOrder("plannedStart", r.PlannedStart, "plannedEnd", r.PlannedEnd).ThrowIfInvalid();
        if (!await _db.Programmes.AnyAsync(p => p.Id == r.ProgrammeId, ct)) throw new ValidationException("programmeId", "Programme not found.");
    }

    private static void Apply(Project p, SaveProjectRequest r)
    {
        p.Name = r.Name.Trim();
        p.Description = r.Description;
        p.ProgrammeId = r.ProgrammeId;
        p.SponsorUserId = r.SponsorUserId;
        p.SponsorName = r.SponsorName;
        if (r.ManagerUserId is not null || r.ManagerName is not null)
        {
            p.ManagerUserId = r.ManagerUserId;
            p.ManagerName = r.ManagerName;
        }
        p.BusinessOwner = r.BusinessOwner;
        p.OrgUnit = r.OrgUnit;
        p.ProjectType = r.ProjectType;
        p.Province = r.Province;
        p.District = r.District;
        p.Municipality = r.Municipality;
        p.PlannedStart = r.PlannedStart;
        p.PlannedEnd = r.PlannedEnd;
    }

    private static BusinessCaseDto ToDto(BusinessCase b) =>
        new(b.Id, b.ProjectId, b.VersionNumber, b.Problem, b.Objectives, b.Options, b.Scope, b.Benefits, b.EstimatedCost, b.Risks, b.DeliveryModel,
            b.ExpectedBenefitMeasure, b.ExpectedBenefitValue, b.Status.ToString(), b.SubmittedAtUtc, b.DecidedAtUtc, b.DecisionComment,
            b.MissingMandatoryFields(), b.Version);

    private static CharterDto ToDto(ProjectCharter c) =>
        new(c.Id, c.VersionNumber, c.Purpose, c.Scope, c.GovernanceStructure, c.KeyMilestones, c.Assumptions, c.Status.ToString(), c.ApprovedAtUtc,
            c.ApprovedBy, c.Version);

    private static GateReviewDto ToDto(StageGateReview r) =>
        new(r.Id, r.FromStage.ToString(), r.ToStage.ToString(), r.Decision.ToString(), r.Comment, r.DecidedAtUtc, r.DecidedBy,
            r.Checks.All(c => !c.IsMandatory || c.IsMet),
            r.Checks.Select(c => new GateCheckDto(c.CriterionId, c.CriterionCode, c.CriterionDescription, c.IsMandatory, c.IsMet, c.AutoEvaluated,
                c.Evidence)).ToList());
}

/// <summary>Business case approval completes → only now is the active, immutable Project ID generated (BR-001, FR-PPM-004/005).</summary>
public sealed class BusinessCaseApprovalHandler : IWorkflowCompletionHandler
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;
    private readonly INumberGenerator _numbers;

    public BusinessCaseApprovalHandler(ITetaDbContext db, IClock clock, INumberGenerator numbers)
    {
        _db = db;
        _clock = clock;
        _numbers = numbers;
    }

    public string EntityType => nameof(BusinessCase);

    public async Task OnCompletedAsync(WorkflowInstance instance, WorkflowState outcome, string? comment, CancellationToken ct)
    {
        var bc = await _db.BusinessCases.SingleAsync(b => b.Id == instance.EntityId, ct);
        var project = await _db.Projects.SingleAsync(p => p.Id == bc.ProjectId, ct);
        var fromStatus = project.Status;
        var fromStage = project.Stage;
        bc.DecidedAtUtc = _clock.UtcNow;
        bc.DecisionComment = comment;

        switch (outcome)
        {
            case WorkflowState.Approved:
                bc.Status = BusinessCaseStatus.Approved;
                project.ActivateWithProjectId(await _numbers.NextAsync(NumberPrefixes.Project, ct), bc.EstimatedCost, _clock.UtcNow);
                break;
            case WorkflowState.Rejected:
                bc.Status = BusinessCaseStatus.Rejected;
                project.Status = ProjectStatus.Rejected;
                break;
            default:
                bc.Status = BusinessCaseStatus.Returned;
                project.Status = ProjectStatus.BusinessCase;
                break;
        }

        _db.ProjectStatusHistory.Add(new ProjectStatusHistory
        {
            ProjectId = project.Id, FromStatus = fromStatus, ToStatus = project.Status, FromStage = fromStage, ToStage = project.Stage,
            ChangedAtUtc = _clock.UtcNow, ChangedBy = "workflow", Reason = $"Business case {outcome}" + (comment is null ? "" : $": {comment}"),
            WorkflowInstanceId = instance.Id
        });
    }
}
