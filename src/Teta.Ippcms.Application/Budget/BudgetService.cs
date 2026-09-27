using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Budget;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Projects;
using Teta.Ippcms.Domain.Scm;

namespace Teta.Ippcms.Application.Budget;

// ---------- Contracts ----------
public sealed record BudgetLineDto(Guid Id, Guid ProjectId, string FinancialYear, string CostCategory, string FundingSource,
    decimal OriginalAmount, decimal RevisedAmount, decimal ForecastAmount, bool IsCarryOver, string? CarryOverFromYear, bool IsBaselined, long Version);
public sealed record SaveBudgetLineRequest(string FinancialYear, string CostCategory, string FundingSource, decimal Amount, bool IsCarryOver,
    string? CarryOverFromYear);
public sealed record ReviseBudgetRequest(decimal NewRevisedAmount, string Reason, Guid? ChangeRequestId);
public sealed record ForecastBudgetRequest(decimal ForecastAmount);
public sealed record BudgetRevisionDto(Guid Id, Guid BudgetLineId, decimal PreviousRevised, decimal NewRevised, string Reason, DateTime RevisedAtUtc, string? RevisedBy);

public sealed record BudgetSummaryDto(Guid ProjectId, decimal ApprovedBudget, decimal TotalOriginal, decimal TotalRevised, decimal TotalForecast,
    bool Reconciled, decimal Committed, decimal PendingRequisitions, decimal Available, IReadOnlyList<BudgetLineDto> Lines,
    IReadOnlyList<YearTotalDto> ByYear);
public sealed record YearTotalDto(string FinancialYear, decimal Original, decimal Revised, decimal Forecast, bool HasCarryOver);

public sealed record AvailabilityResult(bool Available, decimal Budget, decimal Committed, decimal Pending, decimal Remaining, string Message);

public sealed record PlanItemDto(Guid Id, string FinancialYear, Guid ProjectId, string ProjectReference, string ProjectName, Guid? BudgetLineId,
    string Description, decimal EstimatedValue, string? PlannedMethod, DateOnly? PlannedRequisitionDate, DateOnly? PlannedAdvertDate,
    DateOnly? PlannedAwardDate, DateOnly? ActualRequisitionDate, DateOnly? ActualAdvertDate, DateOnly? ActualAwardDate, decimal? ActualValue,
    string Status, string? VarianceReason, int? AdvertVarianceDays, int? AwardVarianceDays, decimal? ValueVariance, string? OrgUnit, long Version);
public sealed record SavePlanItemRequest(string FinancialYear, Guid ProjectId, Guid? BudgetLineId, string Description, decimal EstimatedValue,
    string? PlannedMethod, DateOnly? PlannedRequisitionDate, DateOnly? PlannedAdvertDate, DateOnly? PlannedAwardDate, string? VarianceReason,
    PlanItemStatus? Status, string? OrgUnit);

public sealed record MethodRuleDto(Guid Id, string MethodCode, string Name, decimal MinValue, decimal? MaxValue, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, int RuleVersion, string Status, bool RequiresPublication, int MinimumQuotations, int MinimumAdvertDays,
    string ApprovalAuthority, string? PolicyReference, string? ApprovedBy, DateTime? ApprovedAtUtc, string? CreatedBy, long Version);
public sealed record SaveMethodRuleRequest(string MethodCode, string Name, decimal MinValue, decimal? MaxValue, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, bool RequiresPublication, int MinimumQuotations, int MinimumAdvertDays, string ApprovalAuthority, string? PolicyReference);
public sealed record MethodSelection(Guid RuleId, int RuleVersion, string MethodCode, string Name, bool RequiresPublication, string Rationale);

public sealed record CommitmentForecastDto(string Period, decimal ContractPayments, decimal PlannedProcurement, decimal Total);

public interface IBudgetService
{
    Task<BudgetSummaryDto> GetSummaryAsync(Guid projectId, CancellationToken ct);
    Task<BudgetLineDto> AddLineAsync(Guid projectId, SaveBudgetLineRequest request, CancellationToken ct);
    Task<BudgetLineDto> UpdateLineAsync(Guid projectId, Guid lineId, SaveBudgetLineRequest request, CancellationToken ct);
    Task<BudgetSummaryDto> BaselineAsync(Guid projectId, CancellationToken ct);
    Task<BudgetLineDto> ReviseAsync(Guid projectId, Guid lineId, ReviseBudgetRequest request, CancellationToken ct);
    Task<BudgetLineDto> ForecastAsync(Guid projectId, Guid lineId, ForecastBudgetRequest request, CancellationToken ct);
    Task<IReadOnlyList<BudgetRevisionDto>> GetRevisionsAsync(Guid projectId, CancellationToken ct);
    Task<AvailabilityResult> CheckAvailabilityAsync(Guid projectId, Guid? budgetLineId, decimal amount, Guid? excludeRequisitionId, CancellationToken ct);

    Task<IReadOnlyList<PlanItemDto>> ListPlanAsync(string? financialYear, Guid? projectId, CancellationToken ct);
    Task<PlanItemDto> SavePlanItemAsync(Guid? id, SavePlanItemRequest request, CancellationToken ct);
    Task<int> GenerateDemandPlanAsync(string financialYear, CancellationToken ct);
    Task<IReadOnlyList<CommitmentForecastDto>> CommitmentForecastAsync(string financialYear, CancellationToken ct);

    Task<IReadOnlyList<MethodRuleDto>> ListRulesAsync(CancellationToken ct);
    Task<MethodRuleDto> SaveRuleAsync(Guid? id, SaveMethodRuleRequest request, CancellationToken ct);
    Task<MethodRuleDto> SubmitRuleAsync(Guid id, CancellationToken ct);
    Task<MethodRuleDto> ApproveRuleAsync(Guid id, bool approve, string? comment, CancellationToken ct);
    Task<MethodSelection> SelectMethodAsync(decimal value, DateOnly? onDate, CancellationToken ct);
}

/// <summary>Budget and procurement planning (SRS §5.3).</summary>
public sealed class BudgetService : IBudgetService
{
    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAccessScope _scope;
    private readonly IAuditWriter _audit;

    public BudgetService(ITetaDbContext db, ICurrentUser user, IClock clock, IAccessScope scope, IAuditWriter audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _scope = scope;
        _audit = audit;
    }

    // ----- Project budget (FR-BUD-001/002/010) -----
    public async Task<BudgetSummaryDto> GetSummaryAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        var lines = await _db.BudgetLines.AsNoTracking().Where(b => b.ProjectId == projectId)
            .OrderBy(b => b.FinancialYear).ThenBy(b => b.CostCategory).ToListAsync(ct);
        var availability = await CheckAvailabilityAsync(projectId, null, 0m, null, ct);
        var totalRevised = lines.Sum(l => l.RevisedAmount);
        return new BudgetSummaryDto(projectId, project.ApprovedBudget, lines.Sum(l => l.OriginalAmount), totalRevised, lines.Sum(l => l.ForecastAmount),
            lines.Count > 0 && Math.Abs(totalRevised - project.ApprovedBudget) < 0.01m, availability.Committed, availability.Pending,
            availability.Remaining, lines.Select(ToDto).ToList(),
            lines.GroupBy(l => l.FinancialYear).OrderBy(g => g.Key).Select(g => new YearTotalDto(g.Key, g.Sum(x => x.OriginalAmount),
                g.Sum(x => x.RevisedAmount), g.Sum(x => x.ForecastAmount), g.Any(x => x.IsCarryOver))).ToList());
    }

    public async Task<BudgetLineDto> AddLineAsync(Guid projectId, SaveBudgetLineRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        await ValidateLineAsync(r, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        if (project.IsClosedOrCancelled) throw new DomainException("Budgets cannot be added to a closed or cancelled project.", "FR-BUD-001");
        if (await _db.BudgetLines.AnyAsync(b => b.ProjectId == projectId && b.IsBaselined, ct))
            throw new DomainException("The budget is baselined; new funding must be added through an approved change request/revision.", "FR-BUD-002");
        var line = new ProjectBudgetLine
        {
            ProjectId = projectId, FinancialYear = r.FinancialYear, CostCategory = r.CostCategory, FundingSource = r.FundingSource,
            IsCarryOver = r.IsCarryOver, CarryOverFromYear = r.IsCarryOver ? r.CarryOverFromYear : null
        };
        line.SetOriginal(r.Amount);
        _db.BudgetLines.Add(line);
        await _db.SaveChangesAsync(ct);
        return ToDto(line);
    }

    public async Task<BudgetLineDto> UpdateLineAsync(Guid projectId, Guid lineId, SaveBudgetLineRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        await ValidateLineAsync(r, ct);
        var line = await _db.BudgetLines.SingleOrDefaultAsync(b => b.Id == lineId && b.ProjectId == projectId, ct)
                   ?? throw new NotFoundException("Budget line", lineId);
        line.SetOriginal(r.Amount);   // throws once baselined (immutable original, FR-BUD-002)
        line.FinancialYear = r.FinancialYear;
        line.CostCategory = r.CostCategory;
        line.FundingSource = r.FundingSource;
        line.IsCarryOver = r.IsCarryOver;
        line.CarryOverFromYear = r.IsCarryOver ? r.CarryOverFromYear : null;
        await _db.SaveChangesAsync(ct);
        return ToDto(line);
    }

    public async Task<BudgetSummaryDto> BaselineAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        if (!project.HasProjectId) throw new DomainException("Only an approved project's budget can be baselined.", "BR-001");
        var lines = await _db.BudgetLines.Where(b => b.ProjectId == projectId).ToListAsync(ct);
        if (lines.Count == 0) throw new DomainException("Capture budget lines before baselining.", "FR-BUD-001");
        var total = lines.Sum(l => l.OriginalAmount);
        if (Math.Abs(total - project.ApprovedBudget) >= 0.01m)
            throw new DomainException($"Budget lines total R {total:N2} must reconcile to the approved project budget R {project.ApprovedBudget:N2}.", "FR-BUD-001");
        foreach (var l in lines) l.IsBaselined = true;
        _audit.Write("Budget", nameof(Project), projectId.ToString(), "BudgetBaselined", new { Total = total });
        await _db.SaveChangesAsync(ct);
        return await GetSummaryAsync(projectId, ct);
    }

    public async Task<BudgetLineDto> ReviseAsync(Guid projectId, Guid lineId, ReviseBudgetRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator().NonNegative("newRevisedAmount", r.NewRevisedAmount).Required("reason", r.Reason, 2000).ThrowIfInvalid();
        var line = await _db.BudgetLines.SingleOrDefaultAsync(b => b.Id == lineId && b.ProjectId == projectId, ct)
                   ?? throw new NotFoundException("Budget line", lineId);
        if (!line.IsBaselined) throw new DomainException("Revisions apply to baselined budgets; edit the draft line instead.", "FR-BUD-002");

        var committed = await CommittedAsync(projectId, ct);
        var otherRevised = (await _db.BudgetLines.Where(b => b.ProjectId == projectId && b.Id != lineId).Select(b => b.RevisedAmount).ToListAsync(ct)).Sum();
        if (otherRevised + r.NewRevisedAmount < committed)
            throw new DomainException($"The revised budget cannot fall below existing commitments of R {committed:N2}.", "FR-BUD-005");

        _db.BudgetRevisions.Add(new BudgetRevision
        {
            ProjectId = projectId, BudgetLineId = lineId, PreviousRevised = line.RevisedAmount, NewRevised = r.NewRevisedAmount,
            Reason = r.Reason, ChangeRequestId = r.ChangeRequestId, RevisedAtUtc = _clock.UtcNow, RevisedBy = _user.DisplayName ?? _user.Username
        });
        var delta = r.NewRevisedAmount - line.RevisedAmount;
        line.RevisedAmount = r.NewRevisedAmount;
        line.ForecastAmount = Math.Max(line.ForecastAmount + delta, 0);

        var project = await _db.Projects.SingleAsync(p => p.Id == projectId, ct);
        project.ApprovedBudget += delta;
        await _db.SaveChangesAsync(ct);
        return ToDto(line);
    }

    public async Task<BudgetLineDto> ForecastAsync(Guid projectId, Guid lineId, ForecastBudgetRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator().NonNegative("forecastAmount", r.ForecastAmount).ThrowIfInvalid();
        var line = await _db.BudgetLines.SingleOrDefaultAsync(b => b.Id == lineId && b.ProjectId == projectId, ct)
                   ?? throw new NotFoundException("Budget line", lineId);
        line.ForecastAmount = r.ForecastAmount;
        await _db.SaveChangesAsync(ct);
        return ToDto(line);
    }

    public async Task<IReadOnlyList<BudgetRevisionDto>> GetRevisionsAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        return await _db.BudgetRevisions.AsNoTracking().Where(r => r.ProjectId == projectId).OrderByDescending(r => r.RevisedAtUtc)
            .Select(r => new BudgetRevisionDto(r.Id, r.BudgetLineId, r.PreviousRevised, r.NewRevised, r.Reason, r.RevisedAtUtc, r.RevisedBy))
            .ToListAsync(ct);
    }

    /// <summary>
    /// FR-BUD-005: available = revised budget − active commitments − approved/pending requisitions not yet
    /// contracted. When a budget line is given, the check is for that line's financial year.
    /// </summary>
    public async Task<AvailabilityResult> CheckAvailabilityAsync(Guid projectId, Guid? budgetLineId, decimal amount, Guid? excludeRequisitionId, CancellationToken ct)
    {
        string? fy = null;
        decimal budget;
        if (budgetLineId is { } lineId)
        {
            var line = await _db.BudgetLines.AsNoTracking().SingleOrDefaultAsync(b => b.Id == lineId && b.ProjectId == projectId, ct)
                       ?? throw new ValidationException("budgetLineId", "The budget line does not belong to this project.");
            fy = line.FinancialYear;
            budget = (await _db.BudgetLines.Where(b => b.ProjectId == projectId && b.FinancialYear == fy).Select(b => b.RevisedAmount).ToListAsync(ct)).Sum();
        }
        else
        {
            budget = (await _db.BudgetLines.Where(b => b.ProjectId == projectId).Select(b => b.RevisedAmount).ToListAsync(ct)).Sum();
        }

        var commitmentsQuery = _db.Commitments.Where(c => c.ProjectId == projectId && !c.IsReleased);
        if (fy is not null) commitmentsQuery = commitmentsQuery.Where(c => c.FinancialYear == fy);
        var committed = (await commitmentsQuery.Select(c => c.Amount).ToListAsync(ct)).Sum();

        var pendingQuery = _db.Requisitions.Where(r => r.ProjectId == projectId
            && (r.Status == RequisitionStatus.Submitted || r.Status == RequisitionStatus.Approved || r.Status == RequisitionStatus.Converted)
            && (excludeRequisitionId == null || r.Id != excludeRequisitionId));
        var pendingReqs = await pendingQuery.Select(r => new { r.Id, r.EstimatedValue, r.ProcurementId, r.BudgetLineId }).ToListAsync(ct);

        // Requisitions already converted into a contract are covered by the contract commitment.
        var procurementIds = pendingReqs.Where(r => r.ProcurementId != null).Select(r => r.ProcurementId!.Value).ToList();
        var contracted = await _db.Contracts.Where(c => c.ProcurementId != null && procurementIds.Contains(c.ProcurementId.Value))
            .Select(c => c.ProcurementId!.Value).ToListAsync(ct);
        var lineYears = await _db.BudgetLines.Where(b => b.ProjectId == projectId).Select(b => new { b.Id, b.FinancialYear }).ToListAsync(ct);
        var pending = pendingReqs
            .Where(r => r.ProcurementId is null || !contracted.Contains(r.ProcurementId.Value))
            .Where(r => fy is null || lineYears.Any(l => l.Id == r.BudgetLineId && l.FinancialYear == fy))
            .Sum(r => r.EstimatedValue);

        var remaining = budget - committed - pending;
        var ok = amount <= remaining;
        var message = ok
            ? $"Budget available: R {remaining:N2} remaining{(fy is null ? "" : $" for {fy}")}."
            : $"Insufficient budget: requested R {amount:N2} but only R {remaining:N2} remains{(fy is null ? "" : $" for {fy}")} " +
              $"(budget R {budget:N2}, committed R {committed:N2}, pending R {pending:N2}).";
        return new AvailabilityResult(ok, budget, committed, pending, remaining, message);
    }

    // ----- Procurement / demand plan (FR-BUD-003/004/006/009) -----
    public async Task<IReadOnlyList<PlanItemDto>> ListPlanAsync(string? financialYear, Guid? projectId, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var query = from i in _db.ProcurementPlanItems.AsNoTracking().InScope(scope, x => x.ProjectId)
                    join p in _db.Projects.AsNoTracking() on i.ProjectId equals p.Id
                    select new { i, p };
        if (!string.IsNullOrEmpty(financialYear)) query = query.Where(x => x.i.FinancialYear == financialYear);
        if (projectId is { } pid) query = query.Where(x => x.i.ProjectId == pid);
        var rows = await query.OrderBy(x => x.i.PlannedAdvertDate).ToListAsync(ct);
        return rows.Select(x => ToDto(x.i, x.p)).ToList();
    }

    public async Task<PlanItemDto> SavePlanItemAsync(Guid? id, SavePlanItemRequest r, CancellationToken ct)
    {
        new Validator().Must(Fy.IsValid(r.FinancialYear), "financialYear", "Use the format YYYY/YY.").RequiredId("projectId", r.ProjectId)
            .Required("description", r.Description, 1000).Positive("estimatedValue", r.EstimatedValue)
            .DateOrder("plannedAdvertDate", r.PlannedAdvertDate, "plannedAwardDate", r.PlannedAwardDate).ThrowIfInvalid();
        await _scope.EnsureProjectAsync(r.ProjectId, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == r.ProjectId, ct);
        if (!project.HasProjectId) throw new DomainException("Procurement requirements must reference an approved project.", "FR-BUD-003");
        if (r.BudgetLineId is { } bl && !await _db.BudgetLines.AnyAsync(b => b.Id == bl && b.ProjectId == r.ProjectId, ct))
            throw new ValidationException("budgetLineId", "The budget line does not belong to this project.");

        ProcurementPlanItem item;
        if (id is null)
        {
            item = new ProcurementPlanItem();
            _db.ProcurementPlanItems.Add(item);
        }
        else
        {
            item = await _db.ProcurementPlanItems.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Plan item", id);
        }
        item.FinancialYear = r.FinancialYear;
        item.ProjectId = r.ProjectId;
        item.BudgetLineId = r.BudgetLineId;
        item.Description = r.Description;
        item.EstimatedValue = r.EstimatedValue;
        item.PlannedMethod = r.PlannedMethod ?? (await TrySelectAsync(r.EstimatedValue, ct))?.MethodCode;
        item.PlannedRequisitionDate = r.PlannedRequisitionDate;
        item.PlannedAdvertDate = r.PlannedAdvertDate;
        item.PlannedAwardDate = r.PlannedAwardDate;
        item.VarianceReason = r.VarianceReason;
        item.OrgUnit = r.OrgUnit ?? project.OrgUnit;
        if (r.Status is { } s) item.Status = s;
        await _db.SaveChangesAsync(ct);
        return ToDto(item, project);
    }

    /// <summary>Generates demand-plan lines for approved projects' budget lines that have no plan item yet (FR-BUD-003).</summary>
    public async Task<int> GenerateDemandPlanAsync(string financialYear, CancellationToken ct)
    {
        if (!Fy.IsValid(financialYear)) throw new ValidationException("financialYear", "Use the format YYYY/YY.");
        var scope = await _scope.GetAsync(ct);
        var candidates = await (from b in _db.BudgetLines.AsNoTracking()
                                join p in _db.Projects.AsNoTracking().InScope(scope, p => p.Id) on b.ProjectId equals p.Id
                                where b.FinancialYear == financialYear && p.ProjectNumber != null
                                      && p.Status != ProjectStatus.Cancelled && p.Status != ProjectStatus.Closed
                                      && !_db.ProcurementPlanItems.Any(i => i.BudgetLineId == b.Id)
                                select new { b, p }).ToListAsync(ct);
        var excluded = new[] { "Personnel", "Staff costs", "Compensation of employees" };
        var created = 0;
        foreach (var x in candidates.Where(c => !excluded.Contains(c.b.CostCategory, StringComparer.OrdinalIgnoreCase) && c.b.RevisedAmount > 0))
        {
            var selection = await TrySelectAsync(x.b.RevisedAmount, ct);
            _db.ProcurementPlanItems.Add(new ProcurementPlanItem
            {
                FinancialYear = financialYear, ProjectId = x.p.Id, BudgetLineId = x.b.Id,
                Description = $"{x.p.Name} – {x.b.CostCategory} ({x.b.FundingSource})", EstimatedValue = x.b.RevisedAmount,
                PlannedMethod = selection?.MethodCode, OrgUnit = x.p.OrgUnit
            });
            created++;
        }
        await _db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<IReadOnlyList<CommitmentForecastDto>> CommitmentForecastAsync(string financialYear, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var startYear = int.Parse(financialYear[..4]);
        var fromDate = new DateOnly(startYear, 4, 1);
        var toDate = new DateOnly(startYear + 1, 3, 31);

        var scheduled = await (from s in _db.PaymentScheduleItems.AsNoTracking()
                               join c in _db.Contracts.AsNoTracking().InScope(scope, x => x.ProjectId) on s.ContractId equals c.Id
                               where s.PlannedDate >= fromDate && s.PlannedDate <= toDate && c.Status == ContractStatus.Active
                               select new { s.PlannedDate, s.Amount }).ToListAsync(ct);
        var planned = await _db.ProcurementPlanItems.AsNoTracking().InScope(scope, i => i.ProjectId)
            .Where(i => i.FinancialYear == financialYear && (i.Status == PlanItemStatus.Planned || i.Status == PlanItemStatus.InProgress))
            .Select(i => new { Date = i.PlannedAwardDate, i.EstimatedValue }).ToListAsync(ct);

        var months = Enumerable.Range(0, 12).Select(i => fromDate.AddMonths(i)).ToList();
        return months.Select(m =>
        {
            var key = $"{m.Year}-{m.Month:D2}";
            var pay = scheduled.Where(s => s.PlannedDate.Year == m.Year && s.PlannedDate.Month == m.Month).Sum(s => s.Amount);
            var plan = planned.Where(p => p.Date is { } d && d.Year == m.Year && d.Month == m.Month).Sum(p => p.EstimatedValue);
            return new CommitmentForecastDto(key, pay, plan, pay + plan);
        }).ToList();
    }

    // ----- Method/threshold rules (FR-BUD-007/008, BR-012) -----
    public async Task<IReadOnlyList<MethodRuleDto>> ListRulesAsync(CancellationToken ct) =>
        (await _db.ProcurementMethodRules.AsNoTracking().OrderBy(r => r.MinValue).ThenByDescending(r => r.RuleVersion).ToListAsync(ct))
        .Select(ToDto).ToList();

    public async Task<MethodRuleDto> SaveRuleAsync(Guid? id, SaveMethodRuleRequest r, CancellationToken ct)
    {
        new Validator().Required("methodCode", r.MethodCode, 40).Required("name", r.Name, 200).NonNegative("minValue", r.MinValue)
            .Must(r.MaxValue is null || r.MaxValue >= r.MinValue, "maxValue", "Maximum must be at least the minimum.")
            .DateOrder("effectiveFrom", r.EffectiveFrom, "effectiveTo", r.EffectiveTo)
            .Required("approvalAuthority", r.ApprovalAuthority, 100).ThrowIfInvalid();

        ProcurementMethodRule rule;
        if (id is null)
        {
            var latest = await _db.ProcurementMethodRules.Where(x => x.MethodCode == r.MethodCode).MaxAsync(x => (int?)x.RuleVersion, ct) ?? 0;
            rule = new ProcurementMethodRule { MethodCode = r.MethodCode.Trim(), RuleVersion = latest + 1 };
            _db.ProcurementMethodRules.Add(rule);
        }
        else
        {
            rule = await _db.ProcurementMethodRules.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Method rule", id);
            if (rule.Status != ConfigStatus.Draft)
                throw new DomainException("Approved rules are versioned: create a new version instead of editing.", "BR-012");
        }
        rule.Name = r.Name;
        rule.MinValue = r.MinValue;
        rule.MaxValue = r.MaxValue;
        rule.EffectiveFrom = r.EffectiveFrom;
        rule.EffectiveTo = r.EffectiveTo;
        rule.RequiresPublication = r.RequiresPublication;
        rule.MinimumQuotations = r.MinimumQuotations;
        rule.MinimumAdvertDays = r.MinimumAdvertDays;
        rule.ApprovalAuthority = r.ApprovalAuthority;
        rule.PolicyReference = r.PolicyReference;
        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<MethodRuleDto> SubmitRuleAsync(Guid id, CancellationToken ct)
    {
        var rule = await _db.ProcurementMethodRules.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Method rule", id);
        if (rule.Status != ConfigStatus.Draft) throw new DomainException("Only draft rules can be submitted.", "FR-BUD-008");
        rule.Status = ConfigStatus.PendingApproval;
        rule.SubmittedByUserId = _user.UserId;
        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<MethodRuleDto> ApproveRuleAsync(Guid id, bool approve, string? comment, CancellationToken ct)
    {
        var rule = await _db.ProcurementMethodRules.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Method rule", id);
        if (rule.Status != ConfigStatus.PendingApproval) throw new DomainException("The rule is not awaiting approval.", "FR-BUD-008");
        if (rule.SubmittedByUserId == _user.UserId || rule.CreatedByUserId == _user.UserId)
            throw new DomainException("Configuration changes need approval by a different authorised user.", "FR-BUD-008");
        if (!approve && string.IsNullOrWhiteSpace(comment)) throw new ValidationException("comment", "A reason is required when rejecting.");

        if (approve)
        {
            // Retire overlapping older approved versions of the same method from the new effective date.
            var older = await _db.ProcurementMethodRules
                .Where(x => x.MethodCode == rule.MethodCode && x.Id != rule.Id && x.Status == ConfigStatus.Approved).ToListAsync(ct);
            foreach (var o in older.Where(o => o.EffectiveTo is null || o.EffectiveTo >= rule.EffectiveFrom))
            {
                o.EffectiveTo = rule.EffectiveFrom.AddDays(-1);
                if (o.EffectiveTo < o.EffectiveFrom) o.Status = ConfigStatus.Retired;
            }
            rule.Status = ConfigStatus.Approved;
            rule.ApprovedBy = _user.DisplayName ?? _user.Username;
            rule.ApprovedAtUtc = _clock.UtcNow;
        }
        else
        {
            rule.Status = ConfigStatus.Draft;
        }
        _audit.Write("Admin", nameof(ProcurementMethodRule), rule.Id.ToString(), approve ? "ConfigApproved" : "ConfigRejected",
            new { rule.MethodCode, rule.RuleVersion }, comment);
        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<MethodSelection> SelectMethodAsync(decimal value, DateOnly? onDate, CancellationToken ct) =>
        await TrySelectAsync(value, ct, onDate)
        ?? throw new DomainException($"No approved procurement method rule covers R {value:N2} on {onDate ?? _clock.Today:yyyy-MM-dd}.", "FR-BUD-007");

    private async Task<MethodSelection?> TrySelectAsync(decimal value, CancellationToken ct, DateOnly? onDate = null)
    {
        var date = onDate ?? _clock.Today;
        var rules = await _db.ProcurementMethodRules.AsNoTracking().Where(r => r.Status == ConfigStatus.Approved).ToListAsync(ct);
        var rule = rules.Where(r => r.AppliesTo(value, date)).OrderByDescending(r => r.MinValue).ThenByDescending(r => r.RuleVersion).FirstOrDefault();
        return rule is null ? null : new MethodSelection(rule.Id, rule.RuleVersion, rule.MethodCode, rule.Name, rule.RequiresPublication,
            $"R {value:N2} falls within {rule.Name} (R {rule.MinValue:N0}–{(rule.MaxValue is null ? "∞" : $"R {rule.MaxValue:N0}")}), rule v{rule.RuleVersion} effective {rule.EffectiveFrom:yyyy-MM-dd}" +
            (rule.PolicyReference is null ? "" : $", {rule.PolicyReference}"));
    }

    private async Task<decimal> CommittedAsync(Guid projectId, CancellationToken ct) =>
        (await _db.Commitments.Where(c => c.ProjectId == projectId && !c.IsReleased).Select(c => c.Amount).ToListAsync(ct)).Sum();

    private async Task ValidateLineAsync(SaveBudgetLineRequest r, CancellationToken ct)
    {
        new Validator().Must(Fy.IsValid(r.FinancialYear), "financialYear", "Use the format YYYY/YY.")
            .Required("costCategory", r.CostCategory, 100).Required("fundingSource", r.FundingSource, 100).NonNegative("amount", r.Amount)
            .Must(!r.IsCarryOver || Fy.IsValid(r.CarryOverFromYear), "carryOverFromYear", "Carry-over lines need the originating financial year.")
            .ThrowIfInvalid();
        // Controlled master data (SRS §7.1): categories and sources must exist in reference data.
        var validCategory = await _db.ReferenceData.AnyAsync(x => x.Category == "CostCategory" && x.Name == r.CostCategory && x.IsActive, ct);
        var validSource = await _db.ReferenceData.AnyAsync(x => x.Category == "FundingSource" && x.Name == r.FundingSource && x.IsActive, ct);
        new Validator().Must(validCategory, "costCategory", "Select a cost category from the reference list.")
            .Must(validSource, "fundingSource", "Select a funding source from the reference list.").ThrowIfInvalid();
    }

    private static BudgetLineDto ToDto(ProjectBudgetLine l) =>
        new(l.Id, l.ProjectId, l.FinancialYear, l.CostCategory, l.FundingSource, l.OriginalAmount, l.RevisedAmount, l.ForecastAmount,
            l.IsCarryOver, l.CarryOverFromYear, l.IsBaselined, l.Version);

    private static PlanItemDto ToDto(ProcurementPlanItem i, Project p) =>
        new(i.Id, i.FinancialYear, i.ProjectId, p.Reference, p.Name, i.BudgetLineId, i.Description, i.EstimatedValue, i.PlannedMethod,
            i.PlannedRequisitionDate, i.PlannedAdvertDate, i.PlannedAwardDate, i.ActualRequisitionDate, i.ActualAdvertDate, i.ActualAwardDate,
            i.ActualValue, i.Status.ToString(), i.VarianceReason,
            i.PlannedAdvertDate is { } pa && i.ActualAdvertDate is { } aa ? aa.DayNumber - pa.DayNumber : null,
            i.PlannedAwardDate is { } pw && i.ActualAwardDate is { } aw ? aw.DayNumber - pw.DayNumber : null,
            i.ActualValue is { } av ? av - i.EstimatedValue : null, i.OrgUnit, i.Version);

    private static MethodRuleDto ToDto(ProcurementMethodRule r) =>
        new(r.Id, r.MethodCode, r.Name, r.MinValue, r.MaxValue, r.EffectiveFrom, r.EffectiveTo, r.RuleVersion, r.Status.ToString(),
            r.RequiresPublication, r.MinimumQuotations, r.MinimumAdvertDays, r.ApprovalAuthority, r.PolicyReference, r.ApprovedBy, r.ApprovedAtUtc,
            r.CreatedBy, r.Version);
}
