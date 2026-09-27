using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Documents;
using Teta.Ippcms.Domain.Projects;
using Teta.Ippcms.Domain.Strategy;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.Application.Strategy;

// ---------- Contracts ----------
public sealed record PlanSummaryDto(Guid Id, string Code, string Name, DateOnly PeriodStart, DateOnly PeriodEnd, int VersionNumber,
    string Status, DateTime? ApprovedAtUtc, string? ApprovedBy, int ObjectiveCount, int IndicatorCount, long Version);

public sealed record TargetDto(Guid Id, Guid IndicatorId, string FinancialYear, int Quarter, decimal TargetValue, decimal? ForecastValue,
    string? ForecastCommentary, string Status, long Version);

public sealed record IndicatorDto(Guid Id, Guid ObjectiveId, string Code, string Name, string UnitOfMeasure, string Measure,
    string EvidenceRule, string RequiredEvidenceTypes, string? ResponsibleExecutive, bool IsCumulative, IReadOnlyList<TargetDto> Targets, long Version);

public sealed record ObjectiveDto(Guid Id, Guid? OutcomeId, string Code, string Description, string? OwnerName, string? ProgrammeName,
    IReadOnlyList<IndicatorDto> Indicators, long Version);

public sealed record OutcomeDto(Guid Id, string Code, string Description, long Version);

public sealed record PlanTreeDto(PlanSummaryDto Plan, string? Description, IReadOnlyList<OutcomeDto> Outcomes, IReadOnlyList<ObjectiveDto> Objectives);

public sealed record PlanVersionDto(Guid Id, int VersionNumber, DateTime ApprovedAtUtc, string? ApprovedBy, string? ChangeSummary, string SnapshotJson);

public sealed record SavePlanRequest(string Code, string Name, string? Description, DateOnly PeriodStart, DateOnly PeriodEnd);
public sealed record SaveOutcomeRequest(string Code, string Description);
public sealed record SaveObjectiveRequest(Guid? OutcomeId, string Code, string Description, string? OwnerName, Guid? OwnerUserId, string? ProgrammeName);
public sealed record SaveIndicatorRequest(Guid ObjectiveId, string Code, string Name, string UnitOfMeasure, string Measure, string EvidenceRule,
    string RequiredEvidenceTypes, string? ResponsibleExecutive, Guid? ResponsibleUserId, bool IsCumulative);
public sealed record SaveTargetRequest(Guid IndicatorId, string FinancialYear, int Quarter, decimal TargetValue);
public sealed record ForecastRequest(decimal? ForecastValue, string? ForecastCommentary);
public sealed record LinkIndicatorRequest(Guid IndicatorId, ContributionMethod Method, decimal Weight, decimal? PlannedContribution);
public sealed record IndicatorLinkDto(Guid Id, Guid ProjectId, string ProjectReference, string ProjectName, Guid IndicatorId, string IndicatorCode,
    string IndicatorName, string Method, decimal Weight, decimal? PlannedContribution);

public sealed record CaptureResultRequest(Guid IndicatorId, Guid ProjectId, string FinancialYear, int Quarter, decimal Value, string? Narrative);
public sealed record ResultDto(Guid Id, Guid IndicatorId, string IndicatorCode, Guid ProjectId, string ProjectReference, string FinancialYear, int Quarter,
    decimal Value, decimal Contribution, string? Narrative, string Status, bool IsFinal, string? VerifiedBy, DateTime? VerifiedAtUtc,
    string? VerificationComment, int EvidenceCount, int VerifiedEvidenceCount, IReadOnlyList<string> MissingEvidenceTypes, string? CapturedBy);

public sealed record ContributingProjectDto(Guid ProjectId, string Reference, string Name, decimal VerifiedContribution, decimal PendingContribution,
    int ResultCount);

public sealed record IndicatorPerformanceDto(Guid IndicatorId, string Code, string Name, string UnitOfMeasure, string ObjectiveCode, string FinancialYear,
    int? Quarter, decimal? Target, decimal VerifiedActual, decimal PendingActual, decimal? Forecast, decimal? Variance, decimal? AchievementPercent,
    string? ForecastCommentary, string Status, IReadOnlyList<ContributingProjectDto> ContributingProjects);

// ---------- Service ----------
public interface IStrategyService
{
    Task<IReadOnlyList<PlanSummaryDto>> ListPlansAsync(CancellationToken ct);
    Task<PlanTreeDto> GetPlanAsync(Guid id, CancellationToken ct);
    Task<PlanSummaryDto> CreatePlanAsync(SavePlanRequest request, CancellationToken ct);
    Task<PlanSummaryDto> UpdatePlanAsync(Guid id, SavePlanRequest request, long version, CancellationToken ct);
    Task<WorkflowInstanceDto> SubmitPlanAsync(Guid id, CancellationToken ct);
    Task<PlanSummaryDto> RevisePlanAsync(Guid id, string reason, CancellationToken ct);
    Task<IReadOnlyList<PlanVersionDto>> GetVersionsAsync(Guid planId, CancellationToken ct);

    Task<OutcomeDto> AddOutcomeAsync(Guid planId, SaveOutcomeRequest request, CancellationToken ct);
    Task<ObjectiveDto> AddObjectiveAsync(Guid planId, SaveObjectiveRequest request, CancellationToken ct);
    Task<ObjectiveDto> UpdateObjectiveAsync(Guid objectiveId, SaveObjectiveRequest request, CancellationToken ct);
    Task<IndicatorDto> AddIndicatorAsync(SaveIndicatorRequest request, CancellationToken ct);
    Task<IndicatorDto> UpdateIndicatorAsync(Guid indicatorId, SaveIndicatorRequest request, CancellationToken ct);
    Task<TargetDto> SaveTargetAsync(SaveTargetRequest request, CancellationToken ct);
    Task<WorkflowInstanceDto> SubmitTargetAsync(Guid targetId, CancellationToken ct);
    Task<TargetDto> LockTargetAsync(Guid targetId, CancellationToken ct);
    Task<TargetDto> UpdateForecastAsync(Guid targetId, ForecastRequest request, CancellationToken ct);

    Task<IReadOnlyList<IndicatorLinkDto>> GetProjectLinksAsync(Guid projectId, CancellationToken ct);
    Task<IndicatorLinkDto> LinkProjectAsync(Guid projectId, LinkIndicatorRequest request, CancellationToken ct);
    Task UnlinkProjectAsync(Guid projectId, Guid indicatorId, CancellationToken ct);

    Task<IReadOnlyList<ResultDto>> ListResultsAsync(Guid? indicatorId, Guid? projectId, string? financialYear, CancellationToken ct);
    Task<ResultDto> CaptureResultAsync(CaptureResultRequest request, CancellationToken ct);
    Task<ResultDto> SubmitResultAsync(Guid resultId, CancellationToken ct);
    Task<ResultDto> VerifyResultAsync(Guid resultId, bool approve, string? comment, CancellationToken ct);

    Task<IReadOnlyList<IndicatorPerformanceDto>> GetPerformanceAsync(string financialYear, int? quarter, Guid? planId, CancellationToken ct);
}

/// <summary>Strategy &amp; APP management (SRS §5.1) and APP aggregation (§31, FR-REP-004).</summary>
public sealed class StrategyService : IStrategyService
{
    public const string PlanWorkflow = "STRATEGIC_PLAN_APPROVAL";
    public const string TargetWorkflow = "APP_TARGET_APPROVAL";

    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IWorkflowService _workflow;
    private readonly IAccessScope _scope;
    private readonly ITransactionLedger _ledger;
    private readonly ISodService _sod;
    private readonly IAuditWriter _audit;

    public StrategyService(ITetaDbContext db, ICurrentUser user, IClock clock, IWorkflowService workflow, IAccessScope scope,
        ITransactionLedger ledger, ISodService sod, IAuditWriter audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _workflow = workflow;
        _scope = scope;
        _ledger = ledger;
        _sod = sod;
        _audit = audit;
    }

    // ----- Plans -----
    public async Task<IReadOnlyList<PlanSummaryDto>> ListPlansAsync(CancellationToken ct)
    {
        var plans = await _db.StrategicPlans.AsNoTracking().OrderByDescending(p => p.PeriodStart).ToListAsync(ct);
        var result = new List<PlanSummaryDto>();
        foreach (var p in plans) result.Add(await SummaryAsync(p, ct));
        return result;
    }

    public async Task<PlanTreeDto> GetPlanAsync(Guid id, CancellationToken ct)
    {
        var plan = await _db.StrategicPlans.AsNoTracking()
            .Include(p => p.Outcomes)
            .Include(p => p.Objectives).ThenInclude(o => o.Indicators).ThenInclude(i => i.Targets)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Strategic plan", id);

        return new PlanTreeDto(await SummaryAsync(plan, ct), plan.Description,
            plan.Outcomes.OrderBy(o => o.Code).Select(o => new OutcomeDto(o.Id, o.Code, o.Description, o.Version)).ToList(),
            plan.Objectives.OrderBy(o => o.Code).Select(ToDto).ToList());
    }

    public async Task<PlanSummaryDto> CreatePlanAsync(SavePlanRequest r, CancellationToken ct)
    {
        ValidatePlan(r);
        if (await _db.StrategicPlans.AnyAsync(p => p.Code == r.Code, ct))
            throw new ConflictException($"A strategic plan with code {r.Code} already exists.");
        var plan = new StrategicPlan { Code = r.Code.Trim(), Name = r.Name.Trim(), Description = r.Description, PeriodStart = r.PeriodStart, PeriodEnd = r.PeriodEnd };
        _db.StrategicPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return await SummaryAsync(plan, ct);
    }

    public async Task<PlanSummaryDto> UpdatePlanAsync(Guid id, SavePlanRequest r, long version, CancellationToken ct)
    {
        ValidatePlan(r);
        var plan = await _db.StrategicPlans.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Strategic plan", id);
        CheckVersion(plan, version);
        plan.EnsureEditable();
        plan.Name = r.Name.Trim();
        plan.Description = r.Description;
        plan.PeriodStart = r.PeriodStart;
        plan.PeriodEnd = r.PeriodEnd;
        await _db.SaveChangesAsync(ct);
        return await SummaryAsync(plan, ct);
    }

    public async Task<WorkflowInstanceDto> SubmitPlanAsync(Guid id, CancellationToken ct)
    {
        var plan = await _db.StrategicPlans.Include(p => p.Objectives).ThenInclude(o => o.Indicators)
            .SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Strategic plan", id);
        plan.EnsureEditable();
        if (plan.Objectives.Count == 0) throw new DomainException("A plan needs at least one strategic objective before submission.", "FR-STR-002");
        if (plan.Objectives.Any(o => o.Indicators.Count == 0))
            throw new DomainException("Every strategic objective needs at least one APP indicator before submission.", "FR-STR-002");

        plan.Status = PlanStatus.Submitted;
        var instance = await _workflow.StartAsync(new StartWorkflowRequest(PlanWorkflow, nameof(StrategicPlan), plan.Id,
            $"{plan.Code} v{plan.VersionNumber}", plan.Name, null, null, $"strategy/plans/{plan.Id}"), ct);
        await _db.SaveChangesAsync(ct);
        return await _workflow.GetAsync(instance.Id, ct);
    }

    public async Task<PlanSummaryDto> RevisePlanAsync(Guid id, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ValidationException("reason", "A reason for the revision is required.");
        var plan = await _db.StrategicPlans.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Strategic plan", id);
        if (plan.Status != PlanStatus.Approved) throw new DomainException("Only an approved plan can be revised.", "FR-STR-007");
        plan.Status = PlanStatus.Draft;
        plan.VersionNumber += 1;
        _audit.Write("Strategy", nameof(StrategicPlan), plan.Id.ToString(), "RevisionStarted", new { plan.VersionNumber }, reason);
        await _db.SaveChangesAsync(ct);
        return await SummaryAsync(plan, ct);
    }

    public async Task<IReadOnlyList<PlanVersionDto>> GetVersionsAsync(Guid planId, CancellationToken ct) =>
        await _db.StrategicPlanVersions.AsNoTracking().Where(v => v.PlanId == planId).OrderByDescending(v => v.VersionNumber)
            .Select(v => new PlanVersionDto(v.Id, v.VersionNumber, v.ApprovedAtUtc, v.ApprovedBy, v.ChangeSummary, v.SnapshotJson))
            .ToListAsync(ct);

    // ----- Hierarchy -----
    public async Task<OutcomeDto> AddOutcomeAsync(Guid planId, SaveOutcomeRequest r, CancellationToken ct)
    {
        new Validator().Required("code", r.Code, 30).Required("description", r.Description, 2000).ThrowIfInvalid();
        var plan = await EditablePlanAsync(planId, ct);
        if (await _db.StrategicOutcomes.AnyAsync(o => o.PlanId == planId && o.Code == r.Code, ct))
            throw new ConflictException($"Outcome code {r.Code} already exists in this plan.");
        var outcome = new StrategicOutcome { PlanId = plan.Id, Code = r.Code.Trim(), Description = r.Description.Trim() };
        _db.StrategicOutcomes.Add(outcome);
        await _db.SaveChangesAsync(ct);
        return new OutcomeDto(outcome.Id, outcome.Code, outcome.Description, outcome.Version);
    }

    public async Task<ObjectiveDto> AddObjectiveAsync(Guid planId, SaveObjectiveRequest r, CancellationToken ct)
    {
        ValidateObjective(r);
        var plan = await EditablePlanAsync(planId, ct);
        await EnsureOutcomeInPlanAsync(plan.Id, r.OutcomeId, ct);
        if (await _db.StrategicObjectives.AnyAsync(o => o.PlanId == planId && o.Code == r.Code, ct))
            throw new ConflictException($"Objective code {r.Code} already exists in this plan.");
        var objective = new StrategicObjective
        {
            PlanId = plan.Id, OutcomeId = r.OutcomeId, Code = r.Code.Trim(), Description = r.Description.Trim(),
            OwnerName = r.OwnerName, OwnerUserId = r.OwnerUserId, ProgrammeName = r.ProgrammeName
        };
        _db.StrategicObjectives.Add(objective);
        await _db.SaveChangesAsync(ct);
        return ToDto(objective);
    }

    public async Task<ObjectiveDto> UpdateObjectiveAsync(Guid objectiveId, SaveObjectiveRequest r, CancellationToken ct)
    {
        ValidateObjective(r);
        var objective = await _db.StrategicObjectives.Include(o => o.Indicators).ThenInclude(i => i.Targets)
            .SingleOrDefaultAsync(o => o.Id == objectiveId, ct) ?? throw new NotFoundException("Objective", objectiveId);
        await EditablePlanAsync(objective.PlanId, ct);
        await EnsureOutcomeInPlanAsync(objective.PlanId, r.OutcomeId, ct);
        objective.OutcomeId = r.OutcomeId;
        objective.Description = r.Description.Trim();
        objective.OwnerName = r.OwnerName;
        objective.OwnerUserId = r.OwnerUserId;
        objective.ProgrammeName = r.ProgrammeName;
        await _db.SaveChangesAsync(ct);
        return ToDto(objective);
    }

    public async Task<IndicatorDto> AddIndicatorAsync(SaveIndicatorRequest r, CancellationToken ct)
    {
        ValidateIndicator(r);
        var objective = await _db.StrategicObjectives.SingleOrDefaultAsync(o => o.Id == r.ObjectiveId, ct)
                        ?? throw new ValidationException("objectiveId", "The parent strategic objective does not exist.");
        await EditablePlanAsync(objective.PlanId, ct);
        if (await _db.AppIndicators.AnyAsync(i => i.ObjectiveId == r.ObjectiveId && i.Code == r.Code, ct))
            throw new ConflictException($"Indicator code {r.Code} already exists under this objective.");
        var indicator = new AppIndicator();
        Apply(indicator, r);
        indicator.ObjectiveId = objective.Id;
        indicator.Code = r.Code.Trim();
        _db.AppIndicators.Add(indicator);
        await _db.SaveChangesAsync(ct);
        return ToDto(indicator);
    }

    public async Task<IndicatorDto> UpdateIndicatorAsync(Guid indicatorId, SaveIndicatorRequest r, CancellationToken ct)
    {
        ValidateIndicator(r);
        var indicator = await _db.AppIndicators.Include(i => i.Targets).SingleOrDefaultAsync(i => i.Id == indicatorId, ct)
                        ?? throw new NotFoundException("Indicator", indicatorId);
        var objective = await _db.StrategicObjectives.SingleAsync(o => o.Id == indicator.ObjectiveId, ct);
        await EditablePlanAsync(objective.PlanId, ct);
        if (r.ObjectiveId != indicator.ObjectiveId)
        {
            var newParent = await _db.StrategicObjectives.SingleOrDefaultAsync(o => o.Id == r.ObjectiveId, ct);
            if (newParent is null || newParent.PlanId != objective.PlanId)
                throw new DomainException("An indicator can only move to an objective in the same plan.", "FR-STR-002");
            indicator.ObjectiveId = newParent.Id;
        }
        Apply(indicator, r);
        await _db.SaveChangesAsync(ct);
        return ToDto(indicator);
    }

    // ----- Targets -----
    public async Task<TargetDto> SaveTargetAsync(SaveTargetRequest r, CancellationToken ct)
    {
        new Validator()
            .Must(Fy.IsValid(r.FinancialYear), "financialYear", "Use the format YYYY/YY, e.g. 2026/27.")
            .Range("quarter", r.Quarter, 0, 4)
            .NonNegative("targetValue", r.TargetValue)
            .ThrowIfInvalid();
        _ = await _db.AppIndicators.AsNoTracking().SingleOrDefaultAsync(i => i.Id == r.IndicatorId, ct)
            ?? throw new ValidationException("indicatorId", "Indicator not found.");

        var target = await _db.AppTargets.SingleOrDefaultAsync(
            t => t.IndicatorId == r.IndicatorId && t.FinancialYear == r.FinancialYear && t.Quarter == r.Quarter, ct);
        if (target is null)
        {
            target = new AppTarget { IndicatorId = r.IndicatorId, FinancialYear = r.FinancialYear, Quarter = r.Quarter };
            _db.AppTargets.Add(target);
        }
        else
        {
            target.EnsureEditable();
        }
        target.TargetValue = r.TargetValue;
        await _db.SaveChangesAsync(ct);
        return ToDto(target);
    }

    public async Task<WorkflowInstanceDto> SubmitTargetAsync(Guid targetId, CancellationToken ct)
    {
        var target = await _db.AppTargets.SingleOrDefaultAsync(t => t.Id == targetId, ct) ?? throw new NotFoundException("Target", targetId);
        target.EnsureEditable();
        var indicator = await _db.AppIndicators.AsNoTracking().SingleAsync(i => i.Id == target.IndicatorId, ct);
        var period = target.Quarter == 0 ? $"{target.FinancialYear} annual" : $"{target.FinancialYear} Q{target.Quarter}";
        var instance = await _workflow.StartAsync(new StartWorkflowRequest(TargetWorkflow, nameof(AppTarget), target.Id,
            $"{indicator.Code} {period}", $"APP target {indicator.Code} ({period}) = {target.TargetValue:0.##}", null, null, "strategy"), ct);
        await _db.SaveChangesAsync(ct);
        return await _workflow.GetAsync(instance.Id, ct);
    }

    public async Task<TargetDto> LockTargetAsync(Guid targetId, CancellationToken ct)
    {
        var target = await _db.AppTargets.SingleOrDefaultAsync(t => t.Id == targetId, ct) ?? throw new NotFoundException("Target", targetId);
        if (target.Status != TargetStatus.Approved) throw new DomainException("Only approved targets can be locked.", "FR-STR-003");
        target.Status = TargetStatus.Locked;
        await _db.SaveChangesAsync(ct);
        return ToDto(target);
    }

    public async Task<TargetDto> UpdateForecastAsync(Guid targetId, ForecastRequest r, CancellationToken ct)
    {
        new Validator().NonNegative("forecastValue", r.ForecastValue).Optional("forecastCommentary", r.ForecastCommentary, 2000).ThrowIfInvalid();
        var target = await _db.AppTargets.SingleOrDefaultAsync(t => t.Id == targetId, ct) ?? throw new NotFoundException("Target", targetId);
        target.ForecastValue = r.ForecastValue;
        target.ForecastCommentary = r.ForecastCommentary;
        await _db.SaveChangesAsync(ct);
        return ToDto(target);
    }

    // ----- Project alignment -----
    public async Task<IReadOnlyList<IndicatorLinkDto>> GetProjectLinksAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        return await LinksQuery(l => l.ProjectId == projectId).ToListAsync(ct);
    }

    public async Task<IndicatorLinkDto> LinkProjectAsync(Guid projectId, LinkIndicatorRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator().Positive("weight", r.Weight).NonNegative("plannedContribution", r.PlannedContribution).ThrowIfInvalid();
        _ = await _db.AppIndicators.AsNoTracking().SingleOrDefaultAsync(i => i.Id == r.IndicatorId, ct)
            ?? throw new ValidationException("indicatorId", "Indicator not found.");
        var link = await _db.ProjectIndicatorLinks.SingleOrDefaultAsync(l => l.ProjectId == projectId && l.IndicatorId == r.IndicatorId, ct);
        if (link is null)
        {
            link = new ProjectIndicatorLink { ProjectId = projectId, IndicatorId = r.IndicatorId };
            _db.ProjectIndicatorLinks.Add(link);
        }
        link.Method = r.Method;
        link.Weight = r.Weight;
        link.PlannedContribution = r.PlannedContribution;
        await _db.SaveChangesAsync(ct);
        var linkId = link.Id;
        return await LinksQuery(l => l.Id == linkId).SingleAsync(ct);
    }

    public async Task UnlinkProjectAsync(Guid projectId, Guid indicatorId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var link = await _db.ProjectIndicatorLinks.SingleOrDefaultAsync(l => l.ProjectId == projectId && l.IndicatorId == indicatorId, ct)
                   ?? throw new NotFoundException("Indicator link", indicatorId);
        if (await _db.PerformanceResults.AnyAsync(r => r.ProjectId == projectId && r.IndicatorId == indicatorId, ct))
            throw new DomainException("Results have been reported against this alignment; it cannot be removed.", "FR-STR-005");
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        if (project.Status is ProjectStatus.SubmittedForApproval)
            throw new DomainException("Alignment cannot change while the business case is under approval.", "FR-STR-004");
        _db.ProjectIndicatorLinks.Remove(link);
        await _db.SaveChangesAsync(ct);
    }

    // ----- Performance results -----
    public async Task<IReadOnlyList<ResultDto>> ListResultsAsync(Guid? indicatorId, Guid? projectId, string? financialYear, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var query = _db.PerformanceResults.AsNoTracking().InScope(scope, r => r.ProjectId);
        if (indicatorId is { } i) query = query.Where(r => r.IndicatorId == i);
        if (projectId is { } p) query = query.Where(r => r.ProjectId == p);
        if (!string.IsNullOrEmpty(financialYear)) query = query.Where(r => r.FinancialYear == financialYear);
        var results = await query.OrderByDescending(r => r.FinancialYear).ThenByDescending(r => r.Quarter).ToListAsync(ct);
        return await ToDtosAsync(results, ct);
    }

    public async Task<ResultDto> CaptureResultAsync(CaptureResultRequest r, CancellationToken ct)
    {
        new Validator()
            .Must(Fy.IsValid(r.FinancialYear), "financialYear", "Use the format YYYY/YY, e.g. 2026/27.")
            .Range("quarter", r.Quarter, 1, 4)
            .NonNegative("value", r.Value)
            .Optional("narrative", r.Narrative, 4000)
            .ThrowIfInvalid();
        await _scope.EnsureProjectAsync(r.ProjectId, ct);
        var linked = await _db.ProjectIndicatorLinks.AnyAsync(l => l.ProjectId == r.ProjectId && l.IndicatorId == r.IndicatorId, ct);
        if (!linked) throw new DomainException("The project is not aligned to this APP indicator.", "FR-STR-004");

        var result = new PerformanceResult
        {
            IndicatorId = r.IndicatorId, ProjectId = r.ProjectId, FinancialYear = r.FinancialYear, Quarter = r.Quarter,
            Value = r.Value, Narrative = r.Narrative
        };
        _db.PerformanceResults.Add(result);
        _ledger.Record(nameof(PerformanceResult), result.Id, "Capture", r.ProjectId);
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { result }, ct))[0];
    }

    public async Task<ResultDto> SubmitResultAsync(Guid resultId, CancellationToken ct)
    {
        var result = await LoadResultAsync(resultId, ct);
        if (result.Status is not (ResultStatus.Captured or ResultStatus.Rejected))
            throw new DomainException($"Result is {result.Status} and cannot be submitted.", "FR-STR-006");
        result.Status = ResultStatus.Submitted;
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { result }, ct))[0];
    }

    public async Task<ResultDto> VerifyResultAsync(Guid resultId, bool approve, string? comment, CancellationToken ct)
    {
        var result = await LoadResultAsync(resultId, ct);
        if (result.Status != ResultStatus.Submitted)
            throw new DomainException("Only submitted results can be verified.", "FR-STR-006");
        if (!approve && string.IsNullOrWhiteSpace(comment))
            throw new ValidationException("comment", "A reason is required when rejecting a result.");

        var userId = _user.UserId ?? throw new ForbiddenException();
        await _sod.EnsureAllowedAsync(nameof(PerformanceResult), result.Id, "Verify", userId, ct);

        if (approve)
        {
            var missing = await MissingEvidenceAsync(result, ct);
            if (missing.Count > 0)
                throw new DomainException(
                    $"Achievement cannot be marked final until evidence is verified: missing {string.Join(", ", missing)}.", "BR-009");
        }

        result.Status = approve ? ResultStatus.Verified : ResultStatus.Rejected;
        result.VerifiedAtUtc = _clock.UtcNow;
        result.VerifiedBy = _user.DisplayName ?? _user.Username;
        result.VerifiedByUserId = userId;
        result.VerificationComment = comment;
        _ledger.Record(nameof(PerformanceResult), result.Id, "Verify", result.ProjectId);
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { result }, ct))[0];
    }

    // ----- Performance / aggregation (FR-STR-009, FR-REP-004, SRS §31) -----
    public async Task<IReadOnlyList<IndicatorPerformanceDto>> GetPerformanceAsync(string financialYear, int? quarter, Guid? planId, CancellationToken ct)
    {
        var indicatorQuery = from i in _db.AppIndicators.AsNoTracking()
                             join o in _db.StrategicObjectives.AsNoTracking() on i.ObjectiveId equals o.Id
                             select new { Indicator = i, Objective = o };
        if (planId is { } pid) indicatorQuery = indicatorQuery.Where(x => x.Objective.PlanId == pid);
        var indicators = await indicatorQuery.OrderBy(x => x.Objective.Code).ThenBy(x => x.Indicator.Code).ToListAsync(ct);
        var indicatorIds = indicators.Select(x => x.Indicator.Id).ToList();

        var targets = await _db.AppTargets.AsNoTracking()
            .Where(t => indicatorIds.Contains(t.IndicatorId) && t.FinancialYear == financialYear).ToListAsync(ct);
        var resultsQuery = _db.PerformanceResults.AsNoTracking()
            .Where(r => indicatorIds.Contains(r.IndicatorId) && r.FinancialYear == financialYear && r.Status != ResultStatus.Rejected);
        if (quarter is { } q) resultsQuery = resultsQuery.Where(r => r.Quarter == q);
        var results = await resultsQuery.ToListAsync(ct);
        var links = await _db.ProjectIndicatorLinks.AsNoTracking().Where(l => indicatorIds.Contains(l.IndicatorId)).ToListAsync(ct);
        var projectIds = results.Select(r => r.ProjectId).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, p.DraftReference, p.ProjectNumber, p.Name }).ToDictionaryAsync(p => p.Id, ct);

        var list = new List<IndicatorPerformanceDto>();
        foreach (var x in indicators)
        {
            var ind = x.Indicator;
            var target = targets.FirstOrDefault(t => t.IndicatorId == ind.Id && t.Quarter == (quarter ?? 0))
                         ?? (quarter is null ? null : targets.FirstOrDefault(t => t.IndicatorId == ind.Id && t.Quarter == 0));
            var indResults = results.Where(r => r.IndicatorId == ind.Id).ToList();

            decimal Contribution(PerformanceResult r)
            {
                var link = links.FirstOrDefault(l => l.IndicatorId == ind.Id && l.ProjectId == r.ProjectId);
                return link?.ContributionOf(r.Value) ?? r.Value;
            }

            decimal Aggregate(IEnumerable<PerformanceResult> rs)
            {
                var items = rs.ToList();
                if (items.Count == 0) return 0m;
                if (ind.IsCumulative || quarter is not null) return items.Sum(Contribution);
                // Non-cumulative indicators report the latest quarter's total.
                var latest = items.Max(r => r.Quarter);
                return items.Where(r => r.Quarter == latest).Sum(Contribution);
            }

            var verified = Aggregate(indResults.Where(r => r.Status == ResultStatus.Verified));
            var pending = Aggregate(indResults.Where(r => r.Status != ResultStatus.Verified));
            var contributing = indResults.GroupBy(r => r.ProjectId).Select(g =>
            {
                var project = projects.TryGetValue(g.Key, out var p) ? p : null;
                return new ContributingProjectDto(g.Key, project?.ProjectNumber ?? project?.DraftReference ?? "?", project?.Name ?? "?",
                    g.Where(r => r.Status == ResultStatus.Verified).Sum(Contribution),
                    g.Where(r => r.Status != ResultStatus.Verified).Sum(Contribution), g.Count());
            }).OrderByDescending(c => c.VerifiedContribution).ToList();

            decimal? targetValue = target?.TargetValue;
            var variance = targetValue is null ? (decimal?)null : verified - targetValue.Value;
            var achievement = targetValue is > 0 ? Math.Round(verified / targetValue.Value * 100m, 1) : (decimal?)null;
            var status = targetValue is null ? "NoTarget"
                : achievement >= 100 ? "Achieved"
                : (target!.ForecastValue ?? verified + pending) >= targetValue ? "OnTrack"
                : "AtRisk";

            list.Add(new IndicatorPerformanceDto(ind.Id, ind.Code, ind.Name, ind.UnitOfMeasure, x.Objective.Code, financialYear, quarter,
                targetValue, verified, pending, target?.ForecastValue, variance, achievement, target?.ForecastCommentary, status, contributing));
        }
        return list;
    }

    // ----- helpers -----
    internal async Task<IReadOnlyList<string>> MissingEvidenceAsync(PerformanceResult result, CancellationToken ct)
    {
        var indicator = await _db.AppIndicators.AsNoTracking().SingleAsync(i => i.Id == result.IndicatorId, ct);
        var verifiedTypes = await _db.Evidence.AsNoTracking()
            .Where(e => e.ParentType == ParentTypes.PerformanceResult && e.ParentId == result.Id && e.VerificationStatus == EvidenceStatus.Verified)
            .Select(e => e.EvidenceType).Distinct().ToListAsync(ct);
        var required = indicator.RequiredEvidenceTypeList();
        if (required.Count == 0) return verifiedTypes.Count == 0 ? new[] { "any verified evidence" } : Array.Empty<string>();
        return required.Where(t => !verifiedTypes.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    private async Task<PerformanceResult> LoadResultAsync(Guid id, CancellationToken ct)
    {
        var result = await _db.PerformanceResults.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Result", id);
        await _scope.EnsureProjectAsync(result.ProjectId, ct);
        return result;
    }

    private async Task<List<ResultDto>> ToDtosAsync(IReadOnlyCollection<PerformanceResult> results, CancellationToken ct)
    {
        var ids = results.Select(r => r.Id).ToList();
        var indicatorIds = results.Select(r => r.IndicatorId).Distinct().ToList();
        var projectIds = results.Select(r => r.ProjectId).Distinct().ToList();
        var indicators = await _db.AppIndicators.AsNoTracking().Where(i => indicatorIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = p.ProjectNumber ?? p.DraftReference }).ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        var links = await _db.ProjectIndicatorLinks.AsNoTracking()
            .Where(l => projectIds.Contains(l.ProjectId) && indicatorIds.Contains(l.IndicatorId)).ToListAsync(ct);
        var evidence = await _db.Evidence.AsNoTracking()
            .Where(e => e.ParentType == ParentTypes.PerformanceResult && ids.Contains(e.ParentId))
            .Select(e => new { e.ParentId, e.EvidenceType, e.VerificationStatus }).ToListAsync(ct);

        return results.Select(r =>
        {
            var ind = indicators[r.IndicatorId];
            var ev = evidence.Where(e => e.ParentId == r.Id).ToList();
            var verifiedTypes = ev.Where(e => e.VerificationStatus == EvidenceStatus.Verified).Select(e => e.EvidenceType).ToList();
            var required = ind.RequiredEvidenceTypeList();
            var missing = required.Count == 0
                ? (verifiedTypes.Count == 0 ? new List<string> { "any verified evidence" } : new List<string>())
                : required.Where(t => !verifiedTypes.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
            var link = links.FirstOrDefault(l => l.ProjectId == r.ProjectId && l.IndicatorId == r.IndicatorId);
            return new ResultDto(r.Id, r.IndicatorId, ind.Code, r.ProjectId, projects.GetValueOrDefault(r.ProjectId, "?"), r.FinancialYear,
                r.Quarter, r.Value, link?.ContributionOf(r.Value) ?? r.Value, r.Narrative, r.Status.ToString(), r.IsFinal, r.VerifiedBy,
                r.VerifiedAtUtc, r.VerificationComment, ev.Count, verifiedTypes.Count, missing, r.CreatedBy);
        }).ToList();
    }

    private IQueryable<IndicatorLinkDto> LinksQuery(System.Linq.Expressions.Expression<Func<ProjectIndicatorLink, bool>> filter) =>
        from l in _db.ProjectIndicatorLinks.AsNoTracking().Where(filter)
        join p in _db.Projects.AsNoTracking() on l.ProjectId equals p.Id
        join i in _db.AppIndicators.AsNoTracking() on l.IndicatorId equals i.Id
        select new IndicatorLinkDto(l.Id, l.ProjectId, p.ProjectNumber ?? p.DraftReference, p.Name, l.IndicatorId, i.Code, i.Name,
            l.Method.ToString(), l.Weight, l.PlannedContribution);

    private async Task<StrategicPlan> EditablePlanAsync(Guid planId, CancellationToken ct)
    {
        var plan = await _db.StrategicPlans.SingleOrDefaultAsync(p => p.Id == planId, ct) ?? throw new NotFoundException("Strategic plan", planId);
        plan.EnsureEditable();
        return plan;
    }

    private async Task EnsureOutcomeInPlanAsync(Guid planId, Guid? outcomeId, CancellationToken ct)
    {
        if (outcomeId is null) return;
        var ok = await _db.StrategicOutcomes.AnyAsync(o => o.Id == outcomeId && o.PlanId == planId, ct);
        if (!ok) throw new DomainException("The selected outcome does not belong to this plan.", "FR-STR-002");
    }

    private async Task<PlanSummaryDto> SummaryAsync(StrategicPlan p, CancellationToken ct)
    {
        var objectiveIds = await _db.StrategicObjectives.Where(o => o.PlanId == p.Id).Select(o => o.Id).ToListAsync(ct);
        var indicatorCount = await _db.AppIndicators.CountAsync(i => objectiveIds.Contains(i.ObjectiveId), ct);
        return new PlanSummaryDto(p.Id, p.Code, p.Name, p.PeriodStart, p.PeriodEnd, p.VersionNumber, p.Status.ToString(), p.ApprovedAtUtc,
            p.ApprovedBy, objectiveIds.Count, indicatorCount, p.Version);
    }

    private static void CheckVersion(AuditableEntity entity, long version)
    {
        if (version > 0 && entity.Version != version)
            throw new ConflictException("The record was changed by someone else after you loaded it. Reload and try again.");
    }

    private static void ValidatePlan(SavePlanRequest r) =>
        new Validator().Required("code", r.Code, 30).Required("name", r.Name, 250).Optional("description", r.Description, 4000)
            .DateOrder("periodStart", r.PeriodStart, "periodEnd", r.PeriodEnd).ThrowIfInvalid();

    private static void ValidateObjective(SaveObjectiveRequest r) =>
        new Validator().Required("code", r.Code, 30).Required("description", r.Description, 2000).Optional("ownerName", r.OwnerName, 200).ThrowIfInvalid();

    private static void ValidateIndicator(SaveIndicatorRequest r) =>
        new Validator().Required("code", r.Code, 30).Required("name", r.Name, 500).Required("unitOfMeasure", r.UnitOfMeasure, 50)
            .Required("measure", r.Measure, 1000).Required("evidenceRule", r.EvidenceRule, 2000)
            .Optional("requiredEvidenceTypes", r.RequiredEvidenceTypes, 500).ThrowIfInvalid();

    private static void Apply(AppIndicator i, SaveIndicatorRequest r)
    {
        i.Name = r.Name.Trim();
        i.UnitOfMeasure = r.UnitOfMeasure.Trim();
        i.Measure = r.Measure.Trim();
        i.EvidenceRule = r.EvidenceRule.Trim();
        i.RequiredEvidenceTypes = string.Join(",", (r.RequiredEvidenceTypes ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        i.ResponsibleExecutive = r.ResponsibleExecutive;
        i.ResponsibleUserId = r.ResponsibleUserId;
        i.IsCumulative = r.IsCumulative;
    }

    private static ObjectiveDto ToDto(StrategicObjective o) =>
        new(o.Id, o.OutcomeId, o.Code, o.Description, o.OwnerName, o.ProgrammeName, o.Indicators.OrderBy(i => i.Code).Select(ToDto).ToList(), o.Version);

    private static IndicatorDto ToDto(AppIndicator i) =>
        new(i.Id, i.ObjectiveId, i.Code, i.Name, i.UnitOfMeasure, i.Measure, i.EvidenceRule, i.RequiredEvidenceTypes, i.ResponsibleExecutive,
            i.IsCumulative, i.Targets.OrderBy(t => t.FinancialYear).ThenBy(t => t.Quarter).Select(ToDto).ToList(), i.Version);

    private static TargetDto ToDto(AppTarget t) =>
        new(t.Id, t.IndicatorId, t.FinancialYear, t.Quarter, t.TargetValue, t.ForecastValue, t.ForecastCommentary, t.Status.ToString(), t.Version);
}

/// <summary>Plan approval completes: snapshot the approved baseline immutably (FR-STR-007, FR-STR-010).</summary>
public sealed class StrategicPlanApprovalHandler : IWorkflowCompletionHandler
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;

    public StrategicPlanApprovalHandler(ITetaDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public string EntityType => nameof(StrategicPlan);

    public async Task OnCompletedAsync(WorkflowInstance instance, WorkflowState outcome, string? comment, CancellationToken ct)
    {
        var plan = await _db.StrategicPlans
            .Include(p => p.Outcomes)
            .Include(p => p.Objectives).ThenInclude(o => o.Indicators).ThenInclude(i => i.Targets)
            .AsSplitQuery()
            .SingleAsync(p => p.Id == instance.EntityId, ct);

        if (outcome != WorkflowState.Approved)
        {
            plan.Status = PlanStatus.Draft;
            return;
        }

        var lastApprover = instance.Tasks.Where(t => t.Decision == TaskDecision.Approved).OrderByDescending(t => t.DecidedAtUtc).FirstOrDefault();
        plan.Status = PlanStatus.Approved;
        plan.ApprovedAtUtc = _clock.UtcNow;
        plan.ApprovedBy = lastApprover?.DecidedBy;

        var snapshot = new
        {
            plan.Code, plan.Name, plan.Description, plan.PeriodStart, plan.PeriodEnd, plan.VersionNumber,
            Outcomes = plan.Outcomes.Select(o => new { o.Id, o.Code, o.Description }),
            Objectives = plan.Objectives.Select(o => new
            {
                o.Id, o.Code, o.Description, o.OutcomeId, o.OwnerName,
                Indicators = o.Indicators.Select(i => new
                {
                    i.Id, i.Code, i.Name, i.UnitOfMeasure, i.Measure, i.EvidenceRule, i.RequiredEvidenceTypes,
                    Targets = i.Targets.Select(t => new { t.FinancialYear, t.Quarter, t.TargetValue, Status = t.Status.ToString() })
                })
            })
        };
        _db.StrategicPlanVersions.Add(new StrategicPlanVersion
        {
            PlanId = plan.Id,
            VersionNumber = plan.VersionNumber,
            SnapshotJson = JsonSerializer.Serialize(snapshot),
            ApprovedAtUtc = _clock.UtcNow,
            ApprovedBy = plan.ApprovedBy,
            ChangeSummary = comment
        });
    }
}

/// <summary>APP target approval completes (FR-STR-003, FR-STR-010).</summary>
public sealed class AppTargetApprovalHandler : IWorkflowCompletionHandler
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;

    public AppTargetApprovalHandler(ITetaDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public string EntityType => nameof(AppTarget);

    public async Task OnCompletedAsync(WorkflowInstance instance, WorkflowState outcome, string? comment, CancellationToken ct)
    {
        if (outcome != WorkflowState.Approved) return;
        var target = await _db.AppTargets.SingleAsync(t => t.Id == instance.EntityId, ct);
        target.Status = TargetStatus.Approved;
        target.ApprovedAtUtc = _clock.UtcNow;
        target.ApprovedBy = instance.Tasks.Where(t => t.Decision == TaskDecision.Approved).OrderByDescending(t => t.DecidedAtUtc).FirstOrDefault()?.DecidedBy;
    }
}
