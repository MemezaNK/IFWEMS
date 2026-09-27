using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Engines;
using Teta.Ippcms.Application.Projects;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Budget;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Execution;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Projects;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.Application.Execution;

// ---------- DTOs ----------
public sealed record WbsDto(Guid Id, Guid ProjectId, Guid? ParentId, string Type, string Code, string Name, Guid? OwnerUserId, string? OwnerName,
    DateOnly? PlannedStart, DateOnly? PlannedEnd, DateOnly? BaselineStart, DateOnly? BaselineEnd, DateOnly? ForecastEnd, DateOnly? ActualStart,
    DateOnly? ActualEnd, decimal PercentComplete, decimal RolledUpPercent, decimal Weight, bool IsCritical, string? AcceptanceCriteria, bool EvidenceRequired,
    string Status, int SortOrder, bool IsOverdue, int ScheduleVarianceDays, int Depth, long Version);
public sealed record SaveWbsRequest(Guid? ParentId, WbsType Type, string Code, string Name, Guid? OwnerUserId, string? OwnerName, DateOnly? PlannedStart,
    DateOnly? PlannedEnd, decimal Weight, bool IsCritical, string? AcceptanceCriteria, bool EvidenceRequired, int SortOrder);
public sealed record ProgressRequest(decimal PercentComplete, DateOnly? ForecastEnd, DateOnly? ActualStart, DateOnly? ActualEnd, string? Comment);
public sealed record ProgressDto(Guid Id, Guid WbsElementId, DateTime RecordedAtUtc, decimal PercentComplete, DateOnly? ForecastEnd, DateOnly? ActualStart,
    DateOnly? ActualEnd, string? Comment, string? RecordedBy);
public sealed record DependencyDto(Guid Id, Guid PredecessorId, Guid SuccessorId, string Type, int LagDays);
public sealed record SaveDependencyRequest(Guid PredecessorId, Guid SuccessorId, DependencyType Type, int LagDays);
public sealed record BaselineDto(Guid Id, int BaselineNumber, DateTime ApprovedAtUtc, string? ApprovedBy, string? Reason, Guid? ChangeRequestId,
    DateOnly? PlannedEnd, decimal BudgetAtBaseline);
public sealed record GanttDto(Guid ProjectId, string ProjectReference, DateOnly? Start, DateOnly? End, IReadOnlyList<WbsDto> Items,
    IReadOnlyList<DependencyDto> Dependencies, IReadOnlyList<BaselineDto> Baselines);

public sealed record ResourceDto(Guid Id, Guid WbsElementId, string WbsName, Guid ProjectId, Guid? UserId, string ResourceName, bool IsExternal,
    string Role, decimal AllocationPercent);
public sealed record SaveResourceRequest(Guid WbsElementId, Guid? UserId, string ResourceName, bool IsExternal, string Role, decimal AllocationPercent);
public sealed record WorkloadDto(string ResourceName, bool IsExternal, int Assignments, decimal TotalAllocation, bool OverAllocated,
    IReadOnlyList<string> Projects);

public sealed record IssueDto(Guid Id, string Number, Guid ProjectId, string ProjectReference, string Title, string Description, string Severity,
    Guid? OwnerUserId, string OwnerName, string? Action, DateOnly? DueDate, string Status, int EscalationLevel, string? Resolution, bool IsOverdue,
    DateTime CreatedAtUtc, long Version);
public sealed record SaveIssueRequest(Guid ProjectId, string Title, string Description, Severity Severity, Guid? OwnerUserId, string OwnerName,
    string? Action, DateOnly? DueDate, IssueStatus? Status, string? Resolution);

public sealed record DependencyItemDto(Guid Id, Guid ProjectId, string Description, string Direction, string DependsOn, string? Impact, DateOnly? NeededBy,
    string? OwnerName, string Status, long Version);
public sealed record SaveDependencyItemRequest(string Description, DependencyDirection Direction, string DependsOn, string? Impact, DateOnly? NeededBy,
    string? OwnerName, DependencyStatus Status);

public sealed record ChangeRequestDto(Guid Id, string Number, Guid ProjectId, string ProjectReference, Guid? ContractId, string Type, string Title,
    string Description, string Justification, decimal CostImpact, int ScheduleImpactDays, DateOnly? ProposedEndDate, string? BenefitImpact,
    string? ContractImpact, Guid? BudgetLineId, string Status, DateTime? DecidedAtUtc, string? DecidedBy, Guid? ResultingBaselineId,
    Guid? ResultingVariationId, Guid? WorkflowInstanceId, DateTime CreatedAtUtc, string? CreatedBy, long Version);
public sealed record SaveChangeRequest(Guid ProjectId, Guid? ContractId, ChangeType Type, string Title, string Description, string Justification,
    decimal CostImpact, int ScheduleImpactDays, DateOnly? ProposedEndDate, string? BenefitImpact, string? ContractImpact, Guid? BudgetLineId);
public sealed record ImpactAssessmentDto(decimal CurrentBudget, decimal RevisedBudget, DateOnly? CurrentEnd, DateOnly? RevisedEnd,
    decimal? ContractCeiling, decimal? RevisedContractCeiling, bool ExceedsBudgetAvailability, string Summary);

public sealed record HealthDto(Guid ProjectId, string Overall, int ScheduleScore, int CostScore, int RiskScore, int DeliveryScore, int ScheduleVarianceDays,
    decimal CostVariancePercent, int CriticalOpenRisks, int HighOpenRisks, int OverdueMilestones, int OverdueCriticalMilestones, string Explanation,
    DateTime CalculatedAtUtc);

public sealed record CommentDto(Guid Id, string ParentType, Guid ParentId, string Text, string AuthorName, DateTime CreatedAtUtc);
public sealed record AddCommentRequest(string ParentType, Guid ParentId, string Text);

public sealed record ClosureDto(Guid Id, Guid ProjectId, string LessonsLearned, string HandoverNotes, bool FinancialReconciliationConfirmed,
    bool DocumentationComplete, IReadOnlyList<string> OpenItems, string? ApprovedExceptionReference, string Status, DateTime? DecidedAtUtc,
    string? DecidedBy, long Version);
public sealed record SaveClosureRequest(string LessonsLearned, string HandoverNotes, bool FinancialReconciliationConfirmed, bool DocumentationComplete,
    string? ApprovedExceptionReference);

public sealed record BenefitReviewDto(Guid Id, Guid ProjectId, DateOnly ScheduledDate, string ExpectedBenefit, decimal? ExpectedValue, string? ActualBenefit,
    decimal? ActualValue, decimal? RealisationPercent, string? Findings, string Status, DateOnly? CompletedDate, long Version);
public sealed record SaveBenefitReviewRequest(DateOnly ScheduledDate, string? ExpectedBenefit, decimal? ExpectedValue, string? ActualBenefit,
    decimal? ActualValue, string? Findings, BenefitReviewStatus? Status);

public sealed record StatusReportDto(Guid ProjectId, string Reference, string Name, string Status, string Stage, string Health, string HealthExplanation,
    DateTime GeneratedAtUtc, string? ManagerName, DateOnly? PlannedEnd, DateOnly? ForecastEnd, decimal PercentComplete, decimal ApprovedBudget,
    decimal Committed, decimal Actual, decimal EstimateAtCompletion, IReadOnlyList<WbsDto> UpcomingMilestones, IReadOnlyList<WbsDto> OverdueMilestones,
    IReadOnlyList<IssueDto> OpenIssues, IReadOnlyList<RiskSummary> TopRisks, IReadOnlyList<ChangeRequestDto> RecentChanges);
public sealed record RiskSummary(string Number, string Title, string ResidualRating, string OwnerName, DateOnly ReviewDate);

public interface IExecutionService
{
    Task<IReadOnlyList<WbsDto>> GetWbsAsync(Guid projectId, CancellationToken ct);
    Task<WbsDto> SaveWbsAsync(Guid projectId, Guid? id, SaveWbsRequest request, CancellationToken ct);
    Task<WbsDto> RecordProgressAsync(Guid projectId, Guid id, ProgressRequest request, CancellationToken ct);
    Task<IReadOnlyList<ProgressDto>> GetProgressHistoryAsync(Guid projectId, Guid id, CancellationToken ct);
    Task<DependencyDto> AddDependencyAsync(Guid projectId, SaveDependencyRequest request, CancellationToken ct);
    Task RemoveDependencyAsync(Guid projectId, Guid dependencyId, CancellationToken ct);
    Task<BaselineDto> ApproveBaselineAsync(Guid projectId, string reason, CancellationToken ct);
    Task<GanttDto> GetGanttAsync(Guid projectId, CancellationToken ct);

    Task<IReadOnlyList<ResourceDto>> ListResourcesAsync(Guid projectId, CancellationToken ct);
    Task<ResourceDto> SaveResourceAsync(Guid projectId, Guid? id, SaveResourceRequest request, CancellationToken ct);
    Task<IReadOnlyList<WorkloadDto>> WorkloadAsync(CancellationToken ct);

    Task<IReadOnlyList<IssueDto>> ListIssuesAsync(Guid? projectId, bool openOnly, CancellationToken ct);
    Task<IssueDto> SaveIssueAsync(Guid? id, SaveIssueRequest request, CancellationToken ct);
    Task<IReadOnlyList<DependencyItemDto>> ListDependencyItemsAsync(Guid projectId, CancellationToken ct);
    Task<DependencyItemDto> SaveDependencyItemAsync(Guid projectId, Guid? id, SaveDependencyItemRequest request, CancellationToken ct);

    Task<IReadOnlyList<ChangeRequestDto>> ListChangesAsync(Guid? projectId, CancellationToken ct);
    Task<ChangeRequestDto> SaveChangeAsync(Guid? id, SaveChangeRequest request, CancellationToken ct);
    Task<ImpactAssessmentDto> AssessChangeAsync(Guid id, CancellationToken ct);
    Task<WorkflowInstanceDto> SubmitChangeAsync(Guid id, CancellationToken ct);

    Task<HealthDto> CalculateHealthAsync(Guid projectId, CancellationToken ct);
    Task<int> RecalculateAllHealthAsync(CancellationToken ct);
    Task<IReadOnlyList<HealthDto>> HealthHistoryAsync(Guid projectId, CancellationToken ct);

    Task<IReadOnlyList<CommentDto>> ListCommentsAsync(string parentType, Guid parentId, CancellationToken ct);
    Task<CommentDto> AddCommentAsync(AddCommentRequest request, CancellationToken ct);

    Task<ClosureDto> GetClosureAsync(Guid projectId, CancellationToken ct);
    Task<ClosureDto> SaveClosureAsync(Guid projectId, SaveClosureRequest request, CancellationToken ct);
    Task<WorkflowInstanceDto> SubmitClosureAsync(Guid projectId, CancellationToken ct);
    Task<IReadOnlyList<BenefitReviewDto>> ListBenefitReviewsAsync(Guid projectId, CancellationToken ct);
    Task<BenefitReviewDto> SaveBenefitReviewAsync(Guid projectId, Guid? id, SaveBenefitReviewRequest request, CancellationToken ct);

    Task<StatusReportDto> StatusReportAsync(Guid projectId, CancellationToken ct);
}

/// <summary>Project planning and execution (SRS §5.6), BR-005, BR-010, SRS §30, §33.</summary>
public sealed class ExecutionService : IExecutionService
{
    public const string ChangeWorkflow = "CHANGE_REQUEST";
    public const string ClosureWorkflow = "PROJECT_CLOSURE";

    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAccessScope _scope;
    private readonly INumberGenerator _numbers;
    private readonly IWorkflowService _workflow;
    private readonly ISettings _settings;
    private readonly IGateChecks _gateChecks;
    private readonly IAuditWriter _audit;

    public ExecutionService(ITetaDbContext db, ICurrentUser user, IClock clock, IAccessScope scope, INumberGenerator numbers, IWorkflowService workflow,
        ISettings settings, IGateChecks gateChecks, IAuditWriter audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _scope = scope;
        _numbers = numbers;
        _workflow = workflow;
        _settings = settings;
        _gateChecks = gateChecks;
        _audit = audit;
    }

    // =================== WBS & schedule ===================
    public async Task<IReadOnlyList<WbsDto>> GetWbsAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var elements = await _db.WbsElements.AsNoTracking().Where(w => w.ProjectId == projectId).ToListAsync(ct);
        return Flatten(elements);
    }

    public async Task<WbsDto> SaveWbsAsync(Guid projectId, Guid? id, SaveWbsRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator().Required("code", r.Code, 30).Required("name", r.Name, 300).Positive("weight", r.Weight)
            .DateOrder("plannedStart", r.PlannedStart, "plannedEnd", r.PlannedEnd).ThrowIfInvalid();
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        if (project.IsClosedOrCancelled) throw new DomainException("The project is closed.", "FR-EXE-014");

        if (r.ParentId is { } parentId)
        {
            var parent = await _db.WbsElements.AsNoTracking().SingleOrDefaultAsync(w => w.Id == parentId && w.ProjectId == projectId, ct)
                         ?? throw new ValidationException("parentId", "The parent element does not belong to this project.");
            if ((int)r.Type <= (int)parent.Type && !(r.Type == WbsType.Task && parent.Type == WbsType.Task))
                throw new DomainException($"A {r.Type} cannot sit under a {parent.Type} (workstream → deliverable → milestone → activity → task).", "FR-EXE-001");
            if (id is { } self && await IsDescendantAsync(parentId, self, ct))
                throw new DomainException("An element cannot be moved under its own descendant.", "FR-EXE-001");
        }

        WbsElement e;
        if (id is null)
        {
            e = new WbsElement { ProjectId = projectId };
            _db.WbsElements.Add(e);
        }
        else
        {
            e = await _db.WbsElements.SingleOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId, ct) ?? throw new NotFoundException("WBS element", id);
        }
        e.ParentId = r.ParentId;
        e.Type = r.Type;
        e.Code = r.Code.Trim();
        e.Name = r.Name.Trim();
        e.OwnerUserId = r.OwnerUserId;
        e.OwnerName = r.OwnerName;
        e.PlannedStart = r.PlannedStart;
        e.PlannedEnd = r.PlannedEnd;
        e.Weight = r.Weight;
        e.IsCritical = r.IsCritical;
        e.AcceptanceCriteria = r.AcceptanceCriteria;
        e.EvidenceRequired = r.EvidenceRequired;
        e.SortOrder = r.SortOrder;
        await _db.SaveChangesAsync(ct);
        return (await GetWbsAsync(projectId, ct)).Single(x => x.Id == e.Id);
    }

    public async Task<WbsDto> RecordProgressAsync(Guid projectId, Guid id, ProgressRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var e = await _db.WbsElements.SingleOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId, ct) ?? throw new NotFoundException("WBS element", id);
        if (r.PercentComplete >= 100 && e.EvidenceRequired)
        {
            var hasEvidence = await _db.Evidence.AnyAsync(x => x.ParentType == ParentTypes.Milestone && x.ParentId == id
                && x.VerificationStatus != Domain.Documents.EvidenceStatus.Rejected, ct);
            if (!hasEvidence) throw new DomainException("Completion requires evidence for this milestone/element.", "FR-EXE-006");
        }
        e.RecordProgress(r.PercentComplete, r.ForecastEnd, r.ActualStart, r.ActualEnd ?? (r.PercentComplete >= 100 ? _clock.Today : null));
        _db.ProgressUpdates.Add(new ProgressUpdate
        {
            WbsElementId = id, ProjectId = projectId, RecordedAtUtc = _clock.UtcNow, PercentComplete = r.PercentComplete, ForecastEnd = r.ForecastEnd,
            ActualStart = e.ActualStart, ActualEnd = e.ActualEnd, Comment = r.Comment, RecordedBy = _user.DisplayName ?? _user.Username
        });
        await _db.SaveChangesAsync(ct);
        return (await GetWbsAsync(projectId, ct)).Single(x => x.Id == id);
    }

    public async Task<IReadOnlyList<ProgressDto>> GetProgressHistoryAsync(Guid projectId, Guid id, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        return await _db.ProgressUpdates.AsNoTracking().Where(p => p.WbsElementId == id && p.ProjectId == projectId).OrderByDescending(p => p.RecordedAtUtc)
            .Select(p => new ProgressDto(p.Id, p.WbsElementId, p.RecordedAtUtc, p.PercentComplete, p.ForecastEnd, p.ActualStart, p.ActualEnd, p.Comment, p.RecordedBy))
            .ToListAsync(ct);
    }

    public async Task<DependencyDto> AddDependencyAsync(Guid projectId, SaveDependencyRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        if (r.PredecessorId == r.SuccessorId) throw new DomainException("An element cannot depend on itself.", "FR-EXE-003");
        var ids = new[] { r.PredecessorId, r.SuccessorId };
        if (await _db.WbsElements.CountAsync(w => ids.Contains(w.Id) && w.ProjectId == projectId, ct) != 2)
            throw new ValidationException("predecessorId", "Both elements must belong to this project.");
        var edges = await _db.WbsDependencies.Where(d => d.ProjectId == projectId).Select(d => new { d.PredecessorId, d.SuccessorId }).ToListAsync(ct);
        var all = edges.Select(x => (x.PredecessorId, x.SuccessorId)).Append((r.PredecessorId, r.SuccessorId));
        if (ScheduleValidator.FindCycle(all) is { } cycle)
        {
            var names = await _db.WbsElements.Where(w => cycle.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.Code, ct);
            throw new DomainException($"This dependency would create a loop: {string.Join(" → ", cycle.Select(c => names.GetValueOrDefault(c, "?")))}.", "FR-EXE-003");
        }
        var dep = new WbsDependency { ProjectId = projectId, PredecessorId = r.PredecessorId, SuccessorId = r.SuccessorId, Type = r.Type, LagDays = r.LagDays };
        _db.WbsDependencies.Add(dep);
        await _db.SaveChangesAsync(ct);
        return new DependencyDto(dep.Id, dep.PredecessorId, dep.SuccessorId, dep.Type.ToString(), dep.LagDays);
    }

    public async Task RemoveDependencyAsync(Guid projectId, Guid dependencyId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var dep = await _db.WbsDependencies.SingleOrDefaultAsync(d => d.Id == dependencyId && d.ProjectId == projectId, ct)
                  ?? throw new NotFoundException("Dependency", dependencyId);
        _db.WbsDependencies.Remove(dep);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// FR-EXE-002 / BR-005: baseline 0 is the original approved schedule and is never altered; later
    /// baselines come only from approved change requests. Element baseline dates are set only once.
    /// </summary>
    public async Task<BaselineDto> ApproveBaselineAsync(Guid projectId, string reason, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        if (await _db.ScheduleBaselines.AnyAsync(b => b.ProjectId == projectId, ct))
            throw new DomainException("The original baseline already exists; revise it through an approved change request.", "BR-005");
        var project = await _db.Projects.SingleAsync(p => p.Id == projectId, ct);
        if (!project.HasProjectId) throw new DomainException("Only an approved project can baseline its schedule.", "BR-001");
        var elements = await _db.WbsElements.Where(w => w.ProjectId == projectId).ToListAsync(ct);
        if (elements.Count == 0 || elements.Any(e => e.PlannedStart is null || e.PlannedEnd is null))
            throw new DomainException("Every WBS element needs planned start and end dates before baselining.", "FR-EXE-002");
        foreach (var e in elements)
        {
            e.BaselineStart = e.PlannedStart;
            e.BaselineEnd = e.PlannedEnd;
        }
        var baseline = new ScheduleBaseline
        {
            ProjectId = projectId, BaselineNumber = 0, ApprovedAtUtc = _clock.UtcNow, ApprovedBy = _user.DisplayName ?? _user.Username,
            SnapshotJson = Snapshot(elements), Reason = string.IsNullOrWhiteSpace(reason) ? "Original baseline" : reason,
            PlannedEnd = elements.Max(e => e.PlannedEnd), BudgetAtBaseline = project.ApprovedBudget
        };
        _db.ScheduleBaselines.Add(baseline);
        await _db.SaveChangesAsync(ct);
        return ToDto(baseline);
    }

    public async Task<GanttDto> GetGanttAsync(Guid projectId, CancellationToken ct)
    {
        var items = await GetWbsAsync(projectId, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        var deps = await _db.WbsDependencies.AsNoTracking().Where(d => d.ProjectId == projectId)
            .Select(d => new DependencyDto(d.Id, d.PredecessorId, d.SuccessorId, d.Type.ToString(), d.LagDays)).ToListAsync(ct);
        var baselines = (await _db.ScheduleBaselines.AsNoTracking().Where(b => b.ProjectId == projectId).OrderBy(b => b.BaselineNumber).ToListAsync(ct))
            .Select(ToDto).ToList();
        var starts = items.Select(i => i.PlannedStart ?? i.BaselineStart).Where(d => d.HasValue).Select(d => d!.Value).ToList();
        var ends = items.Select(i => i.ForecastEnd ?? i.PlannedEnd ?? i.BaselineEnd).Where(d => d.HasValue).Select(d => d!.Value).ToList();
        return new GanttDto(projectId, project.Reference, starts.Count == 0 ? project.PlannedStart : starts.Min(), ends.Count == 0 ? project.PlannedEnd : ends.Max(),
            items, deps, baselines);
    }

    // =================== Resources (FR-EXE-007) ===================
    public async Task<IReadOnlyList<ResourceDto>> ListResourcesAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        return await (from r in _db.ResourceAssignments.AsNoTracking()
                      join w in _db.WbsElements.AsNoTracking() on r.WbsElementId equals w.Id
                      where r.ProjectId == projectId
                      orderby r.ResourceName
                      select new ResourceDto(r.Id, r.WbsElementId, w.Name, r.ProjectId, r.UserId, r.ResourceName, r.IsExternal, r.Role, r.AllocationPercent))
            .ToListAsync(ct);
    }

    public async Task<ResourceDto> SaveResourceAsync(Guid projectId, Guid? id, SaveResourceRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator().Required("resourceName", r.ResourceName, 200).Required("role", r.Role, 100).Range("allocationPercent", r.AllocationPercent, 1, 100).ThrowIfInvalid();
        if (!await _db.WbsElements.AnyAsync(w => w.Id == r.WbsElementId && w.ProjectId == projectId, ct))
            throw new ValidationException("wbsElementId", "The WBS element does not belong to this project.");
        ResourceAssignment a;
        if (id is null)
        {
            a = new ResourceAssignment { ProjectId = projectId };
            _db.ResourceAssignments.Add(a);
        }
        else
        {
            a = await _db.ResourceAssignments.SingleOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId, ct) ?? throw new NotFoundException("Assignment", id);
        }
        a.WbsElementId = r.WbsElementId;
        a.UserId = r.UserId;
        a.ResourceName = r.ResourceName;
        a.IsExternal = r.IsExternal;
        a.Role = r.Role;
        a.AllocationPercent = r.AllocationPercent;
        await _db.SaveChangesAsync(ct);
        return (await ListResourcesAsync(projectId, ct)).Single(x => x.Id == a.Id);
    }

    public async Task<IReadOnlyList<WorkloadDto>> WorkloadAsync(CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var rows = await (from r in _db.ResourceAssignments.AsNoTracking().InScope(scope, r => r.ProjectId)
                          join w in _db.WbsElements.AsNoTracking() on r.WbsElementId equals w.Id
                          join p in _db.Projects.AsNoTracking() on r.ProjectId equals p.Id
                          where w.Status != WorkStatus.Completed && w.Status != WorkStatus.Cancelled
                          select new { r.ResourceName, r.IsExternal, r.AllocationPercent, Project = p.ProjectNumber ?? p.DraftReference }).ToListAsync(ct);
        return rows.GroupBy(r => (r.ResourceName, r.IsExternal)).Select(g => new WorkloadDto(g.Key.ResourceName, g.Key.IsExternal, g.Count(),
                g.Sum(x => x.AllocationPercent), g.Sum(x => x.AllocationPercent) > 100, g.Select(x => x.Project).Distinct().ToList()))
            .OrderByDescending(w => w.TotalAllocation).ToList();
    }

    // =================== Issues & dependency register ===================
    public async Task<IReadOnlyList<IssueDto>> ListIssuesAsync(Guid? projectId, bool openOnly, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.Issues.AsNoTracking().InScope(scope, i => i.ProjectId);
        if (projectId is { } pid) q = q.Where(i => i.ProjectId == pid);
        if (openOnly) q = q.Where(i => i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress || i.Status == IssueStatus.Escalated);
        var rows = await q.OrderByDescending(i => i.Severity).ThenBy(i => i.DueDate).ToListAsync(ct);
        return await ToIssueDtosAsync(rows, ct);
    }

    public async Task<IssueDto> SaveIssueAsync(Guid? id, SaveIssueRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("projectId", r.ProjectId).Required("title", r.Title, 300).Required("description", r.Description, 4000)
            .Required("ownerName", r.OwnerName, 200).Required("dueDate", r.DueDate).ThrowIfInvalid();
        await _scope.EnsureProjectAsync(r.ProjectId, ct);
        Issue issue;
        if (id is null)
        {
            issue = new Issue { Number = await _numbers.NextAsync(NumberPrefixes.Issue, ct), ProjectId = r.ProjectId };
            _db.Issues.Add(issue);
        }
        else
        {
            issue = await _db.Issues.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Issue", id);
            await _scope.EnsureProjectAsync(issue.ProjectId, ct);
        }
        issue.Title = r.Title;
        issue.Description = r.Description;
        issue.Severity = r.Severity;
        issue.OwnerUserId = r.OwnerUserId;
        issue.OwnerName = r.OwnerName;
        issue.Action = r.Action;
        issue.DueDate = r.DueDate;
        if (r.Status is { } s)
        {
            if (s is IssueStatus.Resolved or IssueStatus.Closed && string.IsNullOrWhiteSpace(r.Resolution))
                throw new ValidationException("resolution", "Record the resolution before resolving/closing an issue.");
            issue.Status = s;
        }
        issue.Resolution = r.Resolution;
        await _db.SaveChangesAsync(ct);
        return (await ToIssueDtosAsync(new[] { issue }, ct))[0];
    }

    public async Task<IReadOnlyList<DependencyItemDto>> ListDependencyItemsAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        return (await _db.ProjectDependencies.AsNoTracking().Where(d => d.ProjectId == projectId).OrderBy(d => d.NeededBy).ToListAsync(ct))
            .Select(ToDto).ToList();
    }

    public async Task<DependencyItemDto> SaveDependencyItemAsync(Guid projectId, Guid? id, SaveDependencyItemRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator().Required("description", r.Description, 2000).Required("dependsOn", r.DependsOn, 300).ThrowIfInvalid();
        ProjectDependency d;
        if (id is null)
        {
            d = new ProjectDependency { ProjectId = projectId };
            _db.ProjectDependencies.Add(d);
        }
        else
        {
            d = await _db.ProjectDependencies.SingleOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId, ct) ?? throw new NotFoundException("Dependency", id);
        }
        d.Description = r.Description;
        d.Direction = r.Direction;
        d.DependsOn = r.DependsOn;
        d.Impact = r.Impact;
        d.NeededBy = r.NeededBy;
        d.OwnerName = r.OwnerName;
        d.Status = r.Status;
        await _db.SaveChangesAsync(ct);
        return ToDto(d);
    }

    // =================== Change control (FR-EXE-010) ===================
    public async Task<IReadOnlyList<ChangeRequestDto>> ListChangesAsync(Guid? projectId, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.ChangeRequests.AsNoTracking().InScope(scope, c => c.ProjectId);
        if (projectId is { } pid) q = q.Where(c => c.ProjectId == pid);
        return await ToChangeDtosAsync(await q.OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct), ct);
    }

    public async Task<ChangeRequestDto> SaveChangeAsync(Guid? id, SaveChangeRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("projectId", r.ProjectId).Required("title", r.Title, 300).Required("description", r.Description, 4000)
            .Required("justification", r.Justification, 4000).Range("scheduleImpactDays", r.ScheduleImpactDays, -3650, 3650).ThrowIfInvalid();
        await _scope.EnsureProjectAsync(r.ProjectId, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == r.ProjectId, ct);
        project.EnsureActive();
        if (r.ContractId is { } cid && !await _db.Contracts.AnyAsync(c => c.Id == cid && c.ProjectId == r.ProjectId, ct))
            throw new ValidationException("contractId", "The contract does not belong to this project.");
        if (r.BudgetLineId is { } bl && !await _db.BudgetLines.AnyAsync(b => b.Id == bl && b.ProjectId == r.ProjectId, ct))
            throw new ValidationException("budgetLineId", "The budget line does not belong to this project.");
        if (r.CostImpact != 0 && r.BudgetLineId is null)
            throw new ValidationException("budgetLineId", "Select the budget line a cost change applies to.");

        ChangeRequest c;
        if (id is null)
        {
            c = new ChangeRequest { Number = await _numbers.NextAsync(NumberPrefixes.ChangeRequest, ct), ProjectId = r.ProjectId };
            _db.ChangeRequests.Add(c);
        }
        else
        {
            c = await _db.ChangeRequests.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Change request", id);
            if (c.Status != ApprovalState.Draft) throw new DomainException("Only draft change requests can be edited.", "FR-EXE-010");
        }
        c.ContractId = r.ContractId;
        c.Type = r.Type;
        c.Title = r.Title;
        c.Description = r.Description;
        c.Justification = r.Justification;
        c.CostImpact = r.CostImpact;
        c.ScheduleImpactDays = r.ScheduleImpactDays;
        c.ProposedEndDate = r.ProposedEndDate;
        c.BenefitImpact = r.BenefitImpact;
        c.ContractImpact = r.ContractImpact;
        c.BudgetLineId = r.BudgetLineId;
        await _db.SaveChangesAsync(ct);
        return (await ToChangeDtosAsync(new[] { c }, ct))[0];
    }

    public async Task<ImpactAssessmentDto> AssessChangeAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.ChangeRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Change request", id);
        await _scope.EnsureProjectAsync(c.ProjectId, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == c.ProjectId, ct);
        var revisedEnd = c.ProposedEndDate ?? project.PlannedEnd?.AddDays(c.ScheduleImpactDays);
        Contract? contract = c.ContractId is { } cid ? await _db.Contracts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == cid, ct) : null;
        var committed = (await _db.Commitments.Where(x => x.ProjectId == c.ProjectId && !x.IsReleased).Select(x => x.Amount).ToListAsync(ct)).Sum();
        var revisedBudget = project.ApprovedBudget + c.CostImpact;
        var exceeds = revisedBudget < committed;
        var summary = $"Budget R {project.ApprovedBudget:N2} → R {revisedBudget:N2}; end {project.PlannedEnd:yyyy-MM-dd} → {revisedEnd:yyyy-MM-dd}" +
                      (contract is null ? "" : $"; contract {contract.ContractNumber} ceiling R {contract.RevisedValue:N2}" +
                                               (c.CostImpact != 0 ? $" → R {contract.RevisedValue + c.CostImpact:N2} (variation required)" : ""));
        return new ImpactAssessmentDto(project.ApprovedBudget, revisedBudget, project.PlannedEnd, revisedEnd, contract?.RevisedValue,
            contract is null ? null : contract.RevisedValue + c.CostImpact, exceeds, summary);
    }

    public async Task<WorkflowInstanceDto> SubmitChangeAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.ChangeRequests.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Change request", id);
        await _scope.EnsureProjectAsync(c.ProjectId, ct);
        if (c.Status != ApprovalState.Draft) throw new DomainException("Only draft change requests can be submitted.", "FR-EXE-010");
        var impact = await AssessChangeAsync(id, ct);
        if (impact.ExceedsBudgetAvailability)
            throw new DomainException("The revised budget would fall below existing commitments.", "FR-BUD-005");
        c.Status = ApprovalState.Pending;
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == c.ProjectId, ct);
        var instance = await _workflow.StartAsync(new StartWorkflowRequest(ChangeWorkflow, nameof(ChangeRequest), c.Id, c.Number,
            $"{c.Type} change on {project.Reference}: {c.Title}", Math.Abs(c.CostImpact), c.ProjectId, $"projects/{c.ProjectId}"), ct);
        c.WorkflowInstanceId = instance.Id;
        await _db.SaveChangesAsync(ct);
        return await _workflow.GetAsync(instance.Id, ct);
    }

    // =================== Health (FR-EXE-011) ===================
    public async Task<HealthDto> CalculateHealthAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var snapshot = await ComputeAndStoreHealthAsync(projectId, ct);
        await _db.SaveChangesAsync(ct);
        return ToDto(snapshot);
    }

    public async Task<int> RecalculateAllHealthAsync(CancellationToken ct)
    {
        var ids = await _db.Projects.Where(p => p.ProjectNumber != null && p.Status != ProjectStatus.Closed && p.Status != ProjectStatus.Cancelled
                                                && p.Status != ProjectStatus.Rejected)
            .Select(p => p.Id).ToListAsync(ct);
        foreach (var id in ids) await ComputeAndStoreHealthAsync(id, ct);
        await _db.SaveChangesAsync(ct);
        return ids.Count;
    }

    public async Task<IReadOnlyList<HealthDto>> HealthHistoryAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        return (await _db.ProjectHealthSnapshots.AsNoTracking().Where(h => h.ProjectId == projectId).OrderByDescending(h => h.CalculatedAtUtc).Take(52)
            .ToListAsync(ct)).Select(ToDto).ToList();
    }

    internal async Task<ProjectHealthSnapshot> ComputeAndStoreHealthAsync(Guid projectId, CancellationToken ct)
    {
        var today = _clock.Today;
        var project = await _db.Projects.SingleAsync(p => p.Id == projectId, ct);
        var elements = await _db.WbsElements.AsNoTracking().Where(w => w.ProjectId == projectId).ToListAsync(ct);
        var milestones = elements.Where(e => e.Type == WbsType.Milestone).ToList();
        var scheduleVariance = elements.Count == 0 ? 0 : elements.Where(e => e.ParentId == null || e.Type == WbsType.Milestone)
            .Select(e => e.ScheduleVarianceDays(today)).DefaultIfEmpty(0).Max();

        var budget = project.ApprovedBudget;
        var latestForecast = await _db.CostForecasts.AsNoTracking().Where(f => f.ProjectId == projectId).OrderByDescending(f => f.RecordedAtUtc).FirstOrDefaultAsync(ct);
        var lineForecast = (await _db.BudgetLines.Where(b => b.ProjectId == projectId).Select(b => b.ForecastAmount).ToListAsync(ct)).Sum();
        var eac = latestForecast?.EstimateAtCompletion ?? (lineForecast > 0 ? lineForecast : budget);
        var costVariancePct = budget <= 0 ? 0m : Math.Round((eac - budget) / budget * 100m, 1);

        var risks = await _db.Risks.AsNoTracking().Where(r => r.ProjectId == projectId && (r.Status == RiskStatus.Open || r.Status == RiskStatus.Treating))
            .Select(r => r.ResidualRating).ToListAsync(ct);

        var metrics = new ProjectMetrics(Math.Max(0, scheduleVariance), Math.Max(0, costVariancePct), risks.Count(r => r == "Critical"),
            risks.Count(r => r == "High"), milestones.Count(m => m.IsOverdue(today)), milestones.Count(m => m.IsCritical && m.IsOverdue(today)));
        var thresholds = new HealthThresholds(
            await _settings.GetIntAsync(SettingKeys.HealthScheduleAmberDays, ct), await _settings.GetIntAsync(SettingKeys.HealthScheduleRedDays, ct),
            await _settings.GetDecimalAsync(SettingKeys.HealthCostAmberPercent, ct), await _settings.GetDecimalAsync(SettingKeys.HealthCostRedPercent, ct),
            await _settings.GetIntAsync(SettingKeys.HealthHighRisksAmber, ct), await _settings.GetIntAsync(SettingKeys.HealthOverdueMilestonesAmber, ct));
        var result = HealthCalculator.Calculate(metrics, thresholds);

        project.Health = result.Overall;
        project.HealthExplanation = result.Explanation;
        project.HealthCalculatedAtUtc = _clock.UtcNow;
        var snapshot = new ProjectHealthSnapshot
        {
            ProjectId = projectId, CalculatedAtUtc = _clock.UtcNow, Overall = result.Overall.ToString(), ScheduleScore = result.ScheduleScore,
            CostScore = result.CostScore, RiskScore = result.RiskScore, DeliveryScore = result.DeliveryScore, ScheduleVarianceDays = metrics.ScheduleVarianceDays,
            CostVariancePercent = metrics.ForecastCostVariancePercent, CriticalOpenRisks = metrics.CriticalOpenRisks, HighOpenRisks = metrics.HighOpenRisks,
            OverdueMilestones = metrics.OverdueMilestones, OverdueCriticalMilestones = metrics.OverdueCriticalMilestones, Explanation = result.Explanation
        };
        _db.ProjectHealthSnapshots.Add(snapshot);
        return snapshot;
    }

    // =================== Collaboration (FR-EXE-013) ===================
    public async Task<IReadOnlyList<CommentDto>> ListCommentsAsync(string parentType, Guid parentId, CancellationToken ct)
    {
        var comments = await _db.Comments.AsNoTracking().Where(c => c.ParentType == parentType && c.ParentId == parentId)
            .OrderBy(c => c.CreatedAtUtc).ToListAsync(ct);
        var projectId = comments.FirstOrDefault()?.ProjectId;
        if (projectId is { } pid) await _scope.EnsureProjectAsync(pid, ct);
        return comments.Select(c => new CommentDto(c.Id, c.ParentType, c.ParentId, c.Text, c.AuthorName, c.CreatedAtUtc)).ToList();
    }

    public async Task<CommentDto> AddCommentAsync(AddCommentRequest r, CancellationToken ct)
    {
        new Validator().OneOf("parentType", r.ParentType, ParentTypes.All).Required("text", r.Text, 4000).ThrowIfInvalid();
        var projectId = await ResolveProjectAsync(r.ParentType, r.ParentId, ct);
        if (projectId is { } pid) await _scope.EnsureProjectAsync(pid, ct);
        var c = new Comment
        {
            ParentType = r.ParentType, ParentId = r.ParentId, ProjectId = projectId, Text = r.Text.Trim(), AuthorUserId = _user.UserId,
            AuthorName = _user.DisplayName ?? _user.Username ?? "?", CreatedAtUtc = _clock.UtcNow
        };
        _db.Comments.Add(c);
        await _db.SaveChangesAsync(ct);
        return new CommentDto(c.Id, c.ParentType, c.ParentId, c.Text, c.AuthorName, c.CreatedAtUtc);
    }

    // =================== Closure & benefits (FR-EXE-014/015, BR-010) ===================
    public async Task<ClosureDto> GetClosureAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var closure = await _db.ProjectClosures.AsNoTracking().Where(c => c.ProjectId == projectId).OrderByDescending(c => c.CreatedAtUtc).FirstOrDefaultAsync(ct)
                      ?? new ProjectClosure { ProjectId = projectId, LessonsLearned = string.Empty, HandoverNotes = string.Empty };
        return ToDto(closure, await _gateChecks.OpenClosureItemsAsync(projectId, ct));
    }

    public async Task<ClosureDto> SaveClosureAsync(Guid projectId, SaveClosureRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator().Required("lessonsLearned", r.LessonsLearned, 8000).Required("handoverNotes", r.HandoverNotes, 8000).ThrowIfInvalid();
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        if (project.Stage is not (ProjectStage.Execution or ProjectStage.CloseOut))
            throw new DomainException("Closure is prepared from execution or close-out.", "FR-EXE-014");
        var closure = await _db.ProjectClosures.Where(c => c.ProjectId == projectId && c.Status != ClosureStatus.Rejected).FirstOrDefaultAsync(ct);
        if (closure is null)
        {
            closure = new ProjectClosure { ProjectId = projectId };
            _db.ProjectClosures.Add(closure);
        }
        else if (closure.Status != ClosureStatus.Draft)
        {
            throw new DomainException($"Closure is {closure.Status}.", "FR-EXE-014");
        }
        closure.LessonsLearned = r.LessonsLearned;
        closure.HandoverNotes = r.HandoverNotes;
        closure.FinancialReconciliationConfirmed = r.FinancialReconciliationConfirmed;
        closure.DocumentationComplete = r.DocumentationComplete;
        closure.ApprovedExceptionReference = r.ApprovedExceptionReference;
        await _db.SaveChangesAsync(ct);
        return ToDto(closure, await _gateChecks.OpenClosureItemsAsync(projectId, ct));
    }

    public async Task<WorkflowInstanceDto> SubmitClosureAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var closure = await _db.ProjectClosures.SingleOrDefaultAsync(c => c.ProjectId == projectId && c.Status == ClosureStatus.Draft, ct)
                      ?? throw new DomainException("Prepare the closure report first.", "FR-EXE-014");
        if (!closure.FinancialReconciliationConfirmed || !closure.DocumentationComplete)
            throw new DomainException("Confirm financial reconciliation and documentation completeness before submitting closure.", "FR-EXE-014");

        var open = await _gateChecks.OpenClosureItemsAsync(projectId, ct);
        if (open.Count > 0)
        {
            var hasException = !string.IsNullOrWhiteSpace(closure.ApprovedExceptionReference) && await _db.ProcurementExceptions.AnyAsync(
                e => e.Number == closure.ApprovedExceptionReference && e.Status == ApprovalState.Approved && e.ProjectId == projectId, ct);
            if (!hasException)
                throw new DomainException($"The project cannot close while mandatory items remain open: {string.Join("; ", open)}.", "BR-010");
            _audit.Write("Projects", nameof(ProjectClosure), closure.Id.ToString(), "ClosureWithException", new { OpenItems = open },
                closure.ApprovedExceptionReference);
        }

        closure.ChecklistJson = JsonSerializer.Serialize(open);
        closure.Status = ClosureStatus.Submitted;
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        var instance = await _workflow.StartAsync(new StartWorkflowRequest(ClosureWorkflow, nameof(ProjectClosure), closure.Id, project.Reference,
            $"Close-out of {project.Name}", project.ApprovedBudget, projectId, $"projects/{projectId}"), ct);
        closure.WorkflowInstanceId = instance.Id;
        await _db.SaveChangesAsync(ct);
        return await _workflow.GetAsync(instance.Id, ct);
    }

    public async Task<IReadOnlyList<BenefitReviewDto>> ListBenefitReviewsAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        return (await _db.BenefitReviews.AsNoTracking().Where(b => b.ProjectId == projectId).OrderBy(b => b.ScheduledDate).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<BenefitReviewDto> SaveBenefitReviewAsync(Guid projectId, Guid? id, SaveBenefitReviewRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        BenefitReview b;
        if (id is null)
        {
            // Expected benefit defaults to the approved business case so actuals compare like-for-like (FR-EXE-015).
            var bc = await _db.BusinessCases.AsNoTracking().Where(x => x.ProjectId == projectId && x.Status == BusinessCaseStatus.Approved)
                .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
            b = new BenefitReview
            {
                ProjectId = projectId, ExpectedBenefit = r.ExpectedBenefit ?? bc?.ExpectedBenefitMeasure ?? bc?.Benefits ?? "As per business case",
                ExpectedValue = r.ExpectedValue ?? bc?.ExpectedBenefitValue
            };
            _db.BenefitReviews.Add(b);
        }
        else
        {
            b = await _db.BenefitReviews.SingleOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId, ct) ?? throw new NotFoundException("Benefit review", id);
            if (r.ExpectedBenefit is not null) b.ExpectedBenefit = r.ExpectedBenefit;
            if (r.ExpectedValue is not null) b.ExpectedValue = r.ExpectedValue;
        }
        b.ScheduledDate = r.ScheduledDate;
        b.ActualBenefit = r.ActualBenefit;
        b.ActualValue = r.ActualValue;
        b.Findings = r.Findings;
        if (r.Status is { } s)
        {
            if (s == BenefitReviewStatus.Completed && (r.ActualBenefit is null || r.Findings is null))
                throw new ValidationException("actualBenefit", "Record the actual benefit and findings to complete the review.");
            b.Status = s;
            if (s == BenefitReviewStatus.Completed) b.CompletedDate ??= _clock.Today;
        }
        await _db.SaveChangesAsync(ct);
        return ToDto(b);
    }

    // =================== Status report (FR-EXE-012) ===================
    public async Task<StatusReportDto> StatusReportAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        var wbs = await GetWbsAsync(projectId, ct);
        var today = _clock.Today;
        var milestones = wbs.Where(w => w.Type == nameof(WbsType.Milestone)).ToList();
        var issues = await ListIssuesAsync(projectId, true, ct);
        var risks = await _db.Risks.AsNoTracking().Where(r => r.ProjectId == projectId && (r.Status == RiskStatus.Open || r.Status == RiskStatus.Treating))
            .OrderByDescending(r => r.ResidualScore).Take(5)
            .Select(r => new RiskSummary(r.Number, r.Title, r.ResidualRating, r.OwnerName, r.ReviewDate)).ToListAsync(ct);
        var changes = (await ListChangesAsync(projectId, ct)).Take(5).ToList();
        var committed = (await _db.Commitments.Where(c => c.ProjectId == projectId && !c.IsReleased).Select(c => c.Amount).ToListAsync(ct)).Sum();
        var actual = (await _db.Expenditures.Where(e => e.ProjectId == projectId).Select(e => e.Amount).ToListAsync(ct)).Sum();
        var eac = await _db.CostForecasts.AsNoTracking().Where(f => f.ProjectId == projectId).OrderByDescending(f => f.RecordedAtUtc)
            .Select(f => (decimal?)f.EstimateAtCompletion).FirstOrDefaultAsync(ct) ?? project.ApprovedBudget;
        var top = wbs.Where(w => w.ParentId == null).ToList();
        var forecastEnd = wbs.Select(w => w.ForecastEnd ?? w.PlannedEnd).Where(d => d.HasValue).Select(d => d!.Value).DefaultIfEmpty().Max();

        return new StatusReportDto(project.Id, project.Reference, project.Name, project.Status.ToString(), project.Stage.ToString(), project.Health.ToString(),
            project.HealthExplanation ?? "Not yet assessed", _clock.UtcNow, project.ManagerName, project.PlannedEnd,
            forecastEnd == default ? null : forecastEnd, ScheduleValidator.RollUp(top.Select(t => (t.RolledUpPercent, t.Weight))),
            project.ApprovedBudget, committed, actual, eac,
            milestones.Where(m => m.Status != nameof(WorkStatus.Completed) && m.PlannedEnd >= today).OrderBy(m => m.PlannedEnd).Take(5).ToList(),
            milestones.Where(m => m.IsOverdue).ToList(), issues.Take(10).ToList(), risks, changes);
    }

    // =================== helpers ===================
    private async Task<Guid?> ResolveProjectAsync(string parentType, Guid parentId, CancellationToken ct) => parentType switch
    {
        ParentTypes.Project => parentId,
        ParentTypes.Contract => await _db.Contracts.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ParentTypes.Deliverable => await _db.Deliverables.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ParentTypes.Milestone => await _db.WbsElements.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ParentTypes.Issue => await _db.Issues.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ParentTypes.ChangeRequest => await _db.ChangeRequests.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ParentTypes.Risk => await _db.Risks.Where(x => x.Id == parentId).Select(x => x.ProjectId).SingleOrDefaultAsync(ct),
        ParentTypes.Procurement => await _db.Procurements.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ParentTypes.Invoice => await _db.Invoices.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ParentTypes.MonitoringVisit => await _db.MonitoringVisits.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ParentTypes.Finding => await _db.Findings.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        _ => null
    };

    private async Task<bool> IsDescendantAsync(Guid candidate, Guid ancestor, CancellationToken ct)
    {
        var parents = await _db.WbsElements.AsNoTracking().Select(w => new { w.Id, w.ParentId }).ToDictionaryAsync(w => w.Id, w => w.ParentId, ct);
        var current = (Guid?)candidate;
        var guard = 0;
        while (current is { } c && guard++ < 1000)
        {
            if (c == ancestor) return true;
            current = parents.GetValueOrDefault(c);
        }
        return false;
    }

    private IReadOnlyList<WbsDto> Flatten(IReadOnlyList<WbsElement> elements)
    {
        var today = _clock.Today;
        var children = elements.ToLookup(e => e.ParentId);
        var rolled = new Dictionary<Guid, decimal>();

        decimal Roll(WbsElement e)
        {
            if (rolled.TryGetValue(e.Id, out var v)) return v;
            var kids = children[e.Id].ToList();
            var value = kids.Count == 0 ? e.PercentComplete : ScheduleValidator.RollUp(kids.Select(k => (Roll(k), k.Weight)));
            rolled[e.Id] = value;
            return value;
        }

        var result = new List<WbsDto>();
        void Walk(Guid? parent, int depth)
        {
            foreach (var e in children[parent].OrderBy(x => x.SortOrder).ThenBy(x => x.Code))
            {
                result.Add(new WbsDto(e.Id, e.ProjectId, e.ParentId, e.Type.ToString(), e.Code, e.Name, e.OwnerUserId, e.OwnerName, e.PlannedStart,
                    e.PlannedEnd, e.BaselineStart, e.BaselineEnd, e.ForecastEnd, e.ActualStart, e.ActualEnd, e.PercentComplete, Roll(e), e.Weight,
                    e.IsCritical, e.AcceptanceCriteria, e.EvidenceRequired, e.Status.ToString(), e.SortOrder, e.IsOverdue(today),
                    e.ScheduleVarianceDays(today), depth, e.Version));
                if (depth < 20) Walk(e.Id, depth + 1);
            }
        }
        Walk(null, 0);
        return result;
    }

    internal static string Snapshot(IEnumerable<WbsElement> elements) =>
        JsonSerializer.Serialize(elements.Select(e => new
        {
            e.Id, e.ParentId, Type = e.Type.ToString(), e.Code, e.Name, e.PlannedStart, e.PlannedEnd, e.Weight, e.IsCritical
        }));

    private async Task<List<IssueDto>> ToIssueDtosAsync(IReadOnlyCollection<Issue> rows, CancellationToken ct)
    {
        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = p.ProjectNumber ?? p.DraftReference }).ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        var today = _clock.Today;
        return rows.Select(i => new IssueDto(i.Id, i.Number, i.ProjectId, projects.GetValueOrDefault(i.ProjectId, "?"), i.Title, i.Description,
            i.Severity.ToString(), i.OwnerUserId, i.OwnerName, i.Action, i.DueDate, i.Status.ToString(), i.EscalationLevel, i.Resolution,
            i.IsOpen && i.DueDate < today, i.CreatedAtUtc, i.Version)).ToList();
    }

    private async Task<List<ChangeRequestDto>> ToChangeDtosAsync(IReadOnlyCollection<ChangeRequest> rows, CancellationToken ct)
    {
        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = p.ProjectNumber ?? p.DraftReference }).ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        return rows.Select(c => new ChangeRequestDto(c.Id, c.Number, c.ProjectId, projects.GetValueOrDefault(c.ProjectId, "?"), c.ContractId, c.Type.ToString(),
            c.Title, c.Description, c.Justification, c.CostImpact, c.ScheduleImpactDays, c.ProposedEndDate, c.BenefitImpact, c.ContractImpact, c.BudgetLineId,
            c.Status.ToString(), c.DecidedAtUtc, c.DecidedBy, c.ResultingBaselineId, c.ResultingVariationId, c.WorkflowInstanceId, c.CreatedAtUtc,
            c.CreatedBy, c.Version)).ToList();
    }

    private static DependencyItemDto ToDto(ProjectDependency d) =>
        new(d.Id, d.ProjectId, d.Description, d.Direction.ToString(), d.DependsOn, d.Impact, d.NeededBy, d.OwnerName, d.Status.ToString(), d.Version);

    private static BaselineDto ToDto(ScheduleBaseline b) =>
        new(b.Id, b.BaselineNumber, b.ApprovedAtUtc, b.ApprovedBy, b.Reason, b.ChangeRequestId, b.PlannedEnd, b.BudgetAtBaseline);

    private static HealthDto ToDto(ProjectHealthSnapshot h) =>
        new(h.ProjectId, h.Overall, h.ScheduleScore, h.CostScore, h.RiskScore, h.DeliveryScore, h.ScheduleVarianceDays, h.CostVariancePercent,
            h.CriticalOpenRisks, h.HighOpenRisks, h.OverdueMilestones, h.OverdueCriticalMilestones, h.Explanation, h.CalculatedAtUtc);

    private static ClosureDto ToDto(ProjectClosure c, IReadOnlyList<string> open) =>
        new(c.Id, c.ProjectId, c.LessonsLearned, c.HandoverNotes, c.FinancialReconciliationConfirmed, c.DocumentationComplete, open,
            c.ApprovedExceptionReference, c.Status.ToString(), c.DecidedAtUtc, c.DecidedBy, c.Version);

    private static BenefitReviewDto ToDto(BenefitReview b) =>
        new(b.Id, b.ProjectId, b.ScheduledDate, b.ExpectedBenefit, b.ExpectedValue, b.ActualBenefit, b.ActualValue, b.RealisationPercent, b.Findings,
            b.Status.ToString(), b.CompletedDate, b.Version);
}

/// <summary>
/// Approved change request → controlled baseline revision while the original is preserved (FR-EXE-010, BR-005):
/// new schedule baseline for schedule changes, budget revision for cost changes, draft contract variation for contract impact.
/// </summary>
public sealed class ChangeRequestApprovalHandler : IWorkflowCompletionHandler
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;
    private readonly INumberGenerator _numbers;

    public ChangeRequestApprovalHandler(ITetaDbContext db, IClock clock, INumberGenerator numbers)
    {
        _db = db;
        _clock = clock;
        _numbers = numbers;
    }

    public string EntityType => nameof(ChangeRequest);

    public async Task OnCompletedAsync(WorkflowInstance instance, WorkflowState outcome, string? comment, CancellationToken ct)
    {
        var c = await _db.ChangeRequests.SingleAsync(x => x.Id == instance.EntityId, ct);
        c.DecidedAtUtc = _clock.UtcNow;
        c.DecidedBy = instance.Tasks.Where(t => t.DecidedAtUtc != null).OrderByDescending(t => t.DecidedAtUtc).FirstOrDefault()?.DecidedBy;
        if (outcome != WorkflowState.Approved)
        {
            c.Status = outcome == WorkflowState.Rejected ? ApprovalState.Rejected : ApprovalState.Draft;
            return;
        }
        c.Status = ApprovalState.Approved;
        var project = await _db.Projects.SingleAsync(p => p.Id == c.ProjectId, ct);

        // Cost impact → budget revision on the selected line; the original budget stays untouched.
        if (c.CostImpact != 0 && c.BudgetLineId is { } lineId)
        {
            var line = await _db.BudgetLines.SingleAsync(b => b.Id == lineId, ct);
            _db.BudgetRevisions.Add(new BudgetRevision
            {
                ProjectId = c.ProjectId, BudgetLineId = lineId, PreviousRevised = line.RevisedAmount, NewRevised = line.RevisedAmount + c.CostImpact,
                Reason = $"{c.Number}: {c.Title}", ChangeRequestId = c.Id, RevisedAtUtc = _clock.UtcNow, RevisedBy = c.DecidedBy
            });
            line.RevisedAmount += c.CostImpact;
            line.ForecastAmount = Math.Max(0, line.ForecastAmount + c.CostImpact);
            project.ApprovedBudget += c.CostImpact;
        }

        // Schedule impact → new baseline revision (baseline N+1), shifting planned end dates of open work.
        if (c.ScheduleImpactDays != 0 || c.ProposedEndDate is not null || c.CostImpact != 0)
        {
            var elements = await _db.WbsElements.Where(w => w.ProjectId == c.ProjectId).ToListAsync(ct);
            var shift = c.ScheduleImpactDays;
            if (shift == 0 && c.ProposedEndDate is { } proposed && project.PlannedEnd is { } currentEnd) shift = proposed.DayNumber - currentEnd.DayNumber;
            if (shift != 0)
            {
                foreach (var e in elements.Where(e => e.Status is not (WorkStatus.Completed or WorkStatus.Cancelled) && e.PlannedEnd is not null))
                {
                    e.PlannedEnd = e.PlannedEnd!.Value.AddDays(shift);
                }
                project.PlannedEnd = project.PlannedEnd?.AddDays(shift);
            }
            var next = (await _db.ScheduleBaselines.Where(b => b.ProjectId == c.ProjectId).MaxAsync(b => (int?)b.BaselineNumber, ct) ?? -1) + 1;
            var baseline = new ScheduleBaseline
            {
                ProjectId = c.ProjectId, BaselineNumber = next, ApprovedAtUtc = _clock.UtcNow, ApprovedBy = c.DecidedBy,
                SnapshotJson = ExecutionService.Snapshot(elements), ChangeRequestId = c.Id, Reason = $"{c.Number}: {c.Title}",
                PlannedEnd = project.PlannedEnd, BudgetAtBaseline = project.ApprovedBudget
            };
            _db.ScheduleBaselines.Add(baseline);
            c.ResultingBaselineId = baseline.Id;
        }

        // Contract impact → draft variation for the contract manager to process through its own approval.
        if (c.ContractId is { } contractId && (c.CostImpact != 0 || c.ScheduleImpactDays != 0))
        {
            var contract = await _db.Contracts.SingleAsync(x => x.Id == contractId, ct);
            var variation = new ContractVariation
            {
                Number = await _numbers.NextAsync(NumberPrefixes.Variation, ct), ContractId = contractId,
                Type = c.CostImpact != 0 && c.ScheduleImpactDays != 0 ? VariationType.Combined : c.CostImpact != 0 ? VariationType.Value : VariationType.Time,
                IsExtension = c.ScheduleImpactDays > 0, Description = c.Title, Reason = $"Approved change request {c.Number}: {c.Justification}",
                Amount = c.CostImpact, Days = Math.Max(0, c.ScheduleImpactDays),
                RevisedEndDate = c.ScheduleImpactDays > 0 ? contract.CurrentEndDate.AddDays(c.ScheduleImpactDays) : null,
                ValueBefore = contract.RevisedValue, EndDateBefore = contract.CurrentEndDate, ChangeRequestId = c.Id
            };
            _db.ContractVariations.Add(variation);
            c.ResultingVariationId = variation.Id;
        }
    }
}

/// <summary>Closure approval → project closes and moves to benefit review (FR-EXE-014).</summary>
public sealed class ProjectClosureApprovalHandler : IWorkflowCompletionHandler
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;

    public ProjectClosureApprovalHandler(ITetaDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public string EntityType => nameof(ProjectClosure);

    public async Task OnCompletedAsync(WorkflowInstance instance, WorkflowState outcome, string? comment, CancellationToken ct)
    {
        var closure = await _db.ProjectClosures.SingleAsync(c => c.Id == instance.EntityId, ct);
        closure.DecidedAtUtc = _clock.UtcNow;
        closure.DecidedBy = instance.Tasks.Where(t => t.DecidedAtUtc != null).OrderByDescending(t => t.DecidedAtUtc).FirstOrDefault()?.DecidedBy;
        if (outcome != WorkflowState.Approved)
        {
            closure.Status = outcome == WorkflowState.Rejected ? ClosureStatus.Rejected : ClosureStatus.Draft;
            return;
        }
        closure.Status = ClosureStatus.Approved;
        var project = await _db.Projects.SingleAsync(p => p.Id == closure.ProjectId, ct);
        var fromStatus = project.Status;
        var fromStage = project.Stage;
        project.Status = ProjectStatus.Closed;
        project.Stage = ProjectStage.BenefitReview;
        project.ActualEnd ??= _clock.Today;
        _db.ProjectStatusHistory.Add(new ProjectStatusHistory
        {
            ProjectId = project.Id, FromStatus = fromStatus, ToStatus = project.Status, FromStage = fromStage, ToStage = project.Stage,
            ChangedAtUtc = _clock.UtcNow, ChangedBy = closure.DecidedBy, Reason = "Project closure approved", WorkflowInstanceId = instance.Id
        });
        if (!await _db.BenefitReviews.AnyAsync(b => b.ProjectId == project.Id, ct))
        {
            var bc = await _db.BusinessCases.AsNoTracking().Where(x => x.ProjectId == project.Id && x.Status == BusinessCaseStatus.Approved)
                .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
            _db.BenefitReviews.Add(new BenefitReview
            {
                ProjectId = project.Id, ScheduledDate = _clock.Today.AddMonths(6),
                ExpectedBenefit = bc?.ExpectedBenefitMeasure ?? bc?.Benefits ?? "As per business case", ExpectedValue = bc?.ExpectedBenefitValue
            });
        }
    }
}
