using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Strategy;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Budget;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Documents;
using Teta.Ippcms.Domain.Execution;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Monitoring;
using Teta.Ippcms.Domain.Projects;
using Teta.Ippcms.Domain.Scm;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Strategy;

namespace Teta.Ippcms.Application.Reporting;

public sealed record ReportFilter(string? FinancialYear = null, int? Quarter = null, Guid? PortfolioId = null, Guid? ProgrammeId = null,
    Guid? ProjectId = null, string? Status = null, DateOnly? From = null, DateOnly? To = null)
{
    public bool HasProjectFilter => PortfolioId is not null || ProgrammeId is not null || ProjectId is not null;

    public IReadOnlyDictionary<string, string?> Describe() => new Dictionary<string, string?>
    {
        ["Financial year"] = FinancialYear,
        ["Quarter"] = Quarter?.ToString(),
        ["Portfolio"] = PortfolioId?.ToString(),
        ["Programme"] = ProgrammeId?.ToString(),
        ["Project"] = ProjectId?.ToString(),
        ["Status"] = Status,
        ["From"] = From?.ToString("yyyy-MM-dd"),
        ["To"] = To?.ToString("yyyy-MM-dd")
    };
}

public sealed record ReportDefinition(string Code, string Name, string Audience, string Frequency, string Permission, string Description);

/// <summary>The standard report catalogue (SRS §11, RPT-001..RPT-015).</summary>
public static class ReportCatalogue
{
    public static readonly IReadOnlyList<ReportDefinition> All = new[]
    {
        new ReportDefinition("RPT-001", "Strategic/APP performance dashboard", "Executive/Board", "Monthly/Quarterly", Permissions.StrategyRead,
            "Targets vs verified actuals per APP indicator with contributing projects."),
        new ReportDefinition("RPT-002", "Project portfolio health report", "PMO/Executive", "Weekly/Monthly", Permissions.PortfolioRead,
            "Health, stage, status and schedule of every project in scope."),
        new ReportDefinition("RPT-003", "Procurement plan vs actual", "SCM/CFO", "Monthly", Permissions.ProcurementRead,
            "Planned vs actual dates and values for procurement plan items."),
        new ReportDefinition("RPT-004", "Procurement ageing and bottlenecks", "SCM/PMO", "Weekly", Permissions.ProcurementRead,
            "Open procurements with age, current stage and ageing threshold breaches."),
        new ReportDefinition("RPT-005", "Contract register and expiry forecast", "Contract Management/CFO", "Monthly", Permissions.ContractRead,
            "All contracts with value, variations and days to expiry."),
        new ReportDefinition("RPT-006", "Contract variations and extensions", "CFO/Executive/Audit", "Monthly", Permissions.ContractRead,
            "Variations with before/after values and dates and cumulative variation percentage."),
        new ReportDefinition("RPT-007", "Project budget/commitment/actual/forecast", "PMO/Finance", "Monthly", Permissions.FinanceRead,
            "Budget, commitments, actual expenditure, accruals and estimate at completion per project."),
        new ReportDefinition("RPT-008", "Invoice/payment certification status", "Finance/Projects", "Weekly", Permissions.FinanceRead,
            "Invoices with certification and payment status and age."),
        new ReportDefinition("RPT-009", "Supplier performance report", "SCM/Contract Management", "Quarterly", Permissions.SupplierRead,
            "Contract performance scores, deliverable acceptance and breaches per supplier."),
        new ReportDefinition("RPT-010", "M&E visits/findings/corrective actions", "M&E/Executive", "Monthly", Permissions.MeRead,
            "Monitoring visits, findings and corrective action status per project."),
        new ReportDefinition("RPT-011", "Risk heat map and overdue mitigations", "Risk/Executive", "Monthly", Permissions.RiskRead,
            "Open risks with inherent/residual ratings and overdue treatments."),
        new ReportDefinition("RPT-012", "Audit findings and action status", "Audit/Executive", "Monthly", Permissions.RiskRead,
            "Audit findings with management response, owner, due date and status."),
        new ReportDefinition("RPT-013", "APP evidence verification report", "Performance/Audit", "Quarterly", Permissions.StrategyRead,
            "Every reported result with its evidence and verification trail."),
        new ReportDefinition("RPT-014", "Project close-out and benefits report", "PMO/Executive", "Quarterly", Permissions.PortfolioRead,
            "Closure status, lessons and benefit realisation reviews."),
        new ReportDefinition("RPT-015", "Data quality and interface reconciliation", "ICT/Data Owners", "Daily/Weekly", Permissions.ReportsRead,
            "Open data-quality issues, interface errors and unbalanced ERP batches.")
    };

    public static ReportDefinition Get(string code) =>
        All.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotFoundException("Report", code);
}

internal sealed record ProjectInfo(Project Project, string ProgrammeName, Guid PortfolioId, string PortfolioName)
{
    public Guid Id => Project.Id;
    public string Reference => Project.Reference;
}

/// <summary>Builds the RPT-xxx datasets. Every dataset is restricted to the supplied row-level scope (SEC-004).</summary>
internal sealed class ReportBuilder
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;
    private readonly ISettings _settings;
    private readonly IStrategyService _strategy;

    public ReportBuilder(ITetaDbContext db, IClock clock, ISettings settings, IStrategyService strategy)
    {
        _db = db;
        _clock = clock;
        _settings = settings;
        _strategy = strategy;
    }

    /// <summary>Projects visible under the scope and filter, plus the effective scope to apply to related records.</summary>
    public async Task<(DataScope Scope, Dictionary<Guid, ProjectInfo> Projects)> ResolveAsync(ReportFilter filter, DataScope scope, CancellationToken ct)
    {
        var projects = await _db.Projects.AsNoTracking().InScope(scope, p => p.Id).ToListAsync(ct);
        var programmes = await _db.Programmes.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        var portfolios = await _db.Portfolios.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);

        var infos = projects.Select(p =>
        {
            programmes.TryGetValue(p.ProgrammeId, out var programme);
            Portfolio? portfolio = null;
            if (programme is not null) portfolios.TryGetValue(programme.PortfolioId, out portfolio);
            return new ProjectInfo(p, programme?.Name ?? "?", programme?.PortfolioId ?? Guid.Empty, portfolio?.Name ?? "?");
        });
        if (filter.ProjectId is { } projectId) infos = infos.Where(i => i.Id == projectId);
        if (filter.ProgrammeId is { } programmeId) infos = infos.Where(i => i.Project.ProgrammeId == programmeId);
        if (filter.PortfolioId is { } portfolioId) infos = infos.Where(i => i.PortfolioId == portfolioId);

        var map = infos.ToDictionary(i => i.Id);
        var effective = scope.All && !filter.HasProjectFilter ? DataScope.Everything : new DataScope(false, map.Keys.ToHashSet());
        return (effective, map);
    }

    public async Task<ReportTable> BuildAsync(string code, ReportFilter filter, DataScope scope, CancellationToken ct)
    {
        var definition = ReportCatalogue.Get(code);
        var (effective, projects) = await ResolveAsync(filter, scope, ct);
        var (columns, rows) = definition.Code switch
        {
            "RPT-001" => await AppPerformanceAsync(filter, ct),
            "RPT-002" => PortfolioHealth(filter, projects),
            "RPT-003" => await ProcurementPlanAsync(filter, effective, projects, ct),
            "RPT-004" => await ProcurementAgeingAsync(filter, effective, projects, ct),
            "RPT-005" => await ContractRegisterAsync(filter, effective, projects, ct),
            "RPT-006" => await VariationsAsync(filter, effective, projects, ct),
            "RPT-007" => await ProjectFinancialsAsync(filter, effective, projects, ct),
            "RPT-008" => await InvoicesAsync(filter, effective, projects, ct),
            "RPT-009" => await SupplierPerformanceAsync(filter, effective, ct),
            "RPT-010" => await MonitoringAsync(filter, effective, projects, ct),
            "RPT-011" => await RisksAsync(filter, effective, projects, ct),
            "RPT-012" => await AuditFindingsAsync(filter, effective, projects, ct),
            "RPT-013" => await EvidenceVerificationAsync(filter, effective, projects, ct),
            "RPT-014" => await CloseOutAsync(filter, effective, projects, ct),
            "RPT-015" => await DataQualityAsync(filter, ct),
            _ => throw new NotFoundException("Report", code)
        };
        return new ReportTable(definition.Code, definition.Name, _clock.UtcNow, columns, rows, filter.Describe());
    }

    private static string Ref(Dictionary<Guid, ProjectInfo> projects, Guid? id) =>
        id is { } g && projects.TryGetValue(g, out var p) ? p.Reference : string.Empty;

    private static bool StatusMatches(ReportFilter filter, string status) =>
        string.IsNullOrWhiteSpace(filter.Status) || string.Equals(filter.Status, status, StringComparison.OrdinalIgnoreCase);

    private static bool InPeriod(ReportFilter filter, DateOnly? date) =>
        (filter.From is null || (date is not null && date >= filter.From)) && (filter.To is null || (date is not null && date <= filter.To));

    // RPT-001
    private async Task<(string[], List<object?[]>)> AppPerformanceAsync(ReportFilter filter, CancellationToken ct)
    {
        var fy = filter.FinancialYear ?? await _settings.GetAsync(SettingKeys.CurrentFinancialYear, ct);
        var performance = await _strategy.GetPerformanceAsync(fy, filter.Quarter, null, ct);
        var columns = new[]
        {
            "Indicator", "Name", "Objective", "Unit", "Financial year", "Quarter", "Target", "Verified actual", "Pending (unverified)",
            "Forecast", "Variance", "Achievement %", "Status", "Contributing projects"
        };
        var rows = performance.Where(p => StatusMatches(filter, p.Status)).Select(p => new object?[]
        {
            p.Code, p.Name, p.ObjectiveCode, p.UnitOfMeasure, p.FinancialYear, p.Quarter, p.Target, p.VerifiedActual, p.PendingActual,
            p.Forecast, p.Variance, p.AchievementPercent, p.Status,
            string.Join("; ", p.ContributingProjects.Select(c => $"{c.Reference} ({c.VerifiedContribution:0.##})"))
        }).ToList();
        return (columns, rows);
    }

    // RPT-002
    private (string[], List<object?[]>) PortfolioHealth(ReportFilter filter, Dictionary<Guid, ProjectInfo> projects)
    {
        var today = _clock.Today;
        var columns = new[]
        {
            "Project", "Name", "Portfolio", "Programme", "Manager", "Status", "Stage", "Health", "Health explanation", "Approved budget",
            "Planned start", "Planned end", "Days to planned end", "Health calculated"
        };
        var rows = projects.Values
            .Where(p => StatusMatches(filter, p.Project.Status.ToString()) || StatusMatches(filter, p.Project.Health.ToString()))
            .OrderBy(p => p.Project.Health == HealthStatus.Red ? 0 : p.Project.Health == HealthStatus.Amber ? 1 : 2).ThenBy(p => p.Reference)
            .Select(p => new object?[]
            {
                p.Reference, p.Project.Name, p.PortfolioName, p.ProgrammeName, p.Project.ManagerName, p.Project.Status.ToString(), p.Project.Stage.ToString(),
                p.Project.Health.ToString(), p.Project.HealthExplanation, p.Project.ApprovedBudget, p.Project.PlannedStart, p.Project.PlannedEnd,
                p.Project.PlannedEnd is { } end ? end.DayNumber - today.DayNumber : null, p.Project.HealthCalculatedAtUtc
            }).ToList();
        return (columns, rows);
    }

    // RPT-003
    private async Task<(string[], List<object?[]>)> ProcurementPlanAsync(ReportFilter filter, DataScope scope, Dictionary<Guid, ProjectInfo> projects, CancellationToken ct)
    {
        var items = await _db.ProcurementPlanItems.AsNoTracking().InScope(scope, i => i.ProjectId)
            .Where(i => filter.FinancialYear == null || i.FinancialYear == filter.FinancialYear).ToListAsync(ct);
        var columns = new[]
        {
            "Financial year", "Project", "Description", "Planned method", "Estimated value", "Actual value", "Value variance",
            "Planned requisition", "Actual requisition", "Planned advert", "Actual advert", "Planned award", "Actual award",
            "Award slippage (days)", "Status", "Variance reason"
        };
        var rows = items.Where(i => projects.ContainsKey(i.ProjectId) && StatusMatches(filter, i.Status.ToString()))
            .OrderBy(i => i.FinancialYear).ThenBy(i => Ref(projects, i.ProjectId))
            .Select(i => new object?[]
            {
                i.FinancialYear, Ref(projects, i.ProjectId), i.Description, i.PlannedMethod, i.EstimatedValue, i.ActualValue,
                i.ActualValue is { } a ? a - i.EstimatedValue : null, i.PlannedRequisitionDate, i.ActualRequisitionDate, i.PlannedAdvertDate,
                i.ActualAdvertDate, i.PlannedAwardDate, i.ActualAwardDate,
                i.PlannedAwardDate is { } pa ? (i.ActualAwardDate ?? _clock.Today).DayNumber - pa.DayNumber : null,
                i.Status.ToString(), i.VarianceReason
            }).ToList();
        return (columns, rows);
    }

    // RPT-004
    private async Task<(string[], List<object?[]>)> ProcurementAgeingAsync(ReportFilter filter, DataScope scope, Dictionary<Guid, ProjectInfo> projects, CancellationToken ct)
    {
        var threshold = await _settings.GetIntAsync(SettingKeys.ExceptionProcurementAgeingDays, ct);
        var open = await _db.Procurements.AsNoTracking().InScope(scope, p => p.ProjectId)
            .Where(p => p.Status != ProcurementStatus.Awarded && p.Status != ProcurementStatus.Cancelled).ToListAsync(ct);
        var ids = open.Select(p => p.Id).ToList();
        var pendingTasks = await (from t in _db.WorkflowTasks.AsNoTracking()
                                  join i in _db.WorkflowInstances.AsNoTracking() on t.InstanceId equals i.Id
                                  where t.Decision == Domain.Workflow.TaskDecision.Pending && ids.Contains(i.EntityId)
                                  select new { i.EntityId, t.StepName, t.AssignedRole, t.DueAtUtc }).ToListAsync(ct);
        var now = _clock.UtcNow;
        var columns = new[]
        {
            "Procurement", "Title", "Project", "Method", "Estimated value", "Status", "Started", "Age (days)", "Days in current stage",
            "Pending approval step", "Approver role", "Approval overdue", "Exceeds ageing threshold"
        };
        var rows = open.Where(p => StatusMatches(filter, p.Status.ToString()))
            .Select(p =>
            {
                var age = (int)(now - p.CreatedAtUtc).TotalDays;
                var inStage = (int)(now - (p.UpdatedAtUtc ?? p.CreatedAtUtc)).TotalDays;
                var task = pendingTasks.Where(t => t.EntityId == p.Id).OrderBy(t => t.DueAtUtc).FirstOrDefault();
                return new object?[]
                {
                    p.Number, p.Title, Ref(projects, p.ProjectId), p.Method, p.EstimatedValue, p.Status.ToString(), p.CreatedAtUtc, age, inStage,
                    task?.StepName, task?.AssignedRole, task is not null && task.DueAtUtc < now, age > threshold
                };
            })
            .OrderByDescending(r => (int)r[7]!).ToList();
        return (columns, rows);
    }

    // RPT-005
    private async Task<(string[], List<object?[]>)> ContractRegisterAsync(ReportFilter filter, DataScope scope, Dictionary<Guid, ProjectInfo> projects, CancellationToken ct)
    {
        var contracts = await _db.Contracts.AsNoTracking().InScope(scope, c => c.ProjectId).ToListAsync(ct);
        var suppliers = await SupplierNamesAsync(contracts.Select(c => c.SupplierId), ct);
        var today = _clock.Today;
        var columns = new[]
        {
            "Contract", "Title", "Supplier", "Project", "Source", "Status", "Original value", "Approved variations", "Revised value",
            "Start", "Original end", "Current end", "Days to expiry", "Expiry band", "Contract manager", "Performance rating"
        };
        var rows = contracts.Where(c => StatusMatches(filter, c.Status.ToString()) && InPeriod(filter, c.CurrentEndDate))
            .OrderBy(c => c.CurrentEndDate)
            .Select(c =>
            {
                var days = c.CurrentEndDate.DayNumber - today.DayNumber;
                var band = c.Status is ContractStatus.Closed or ContractStatus.Terminated ? "Closed"
                    : days < 0 ? "Expired" : days <= 30 ? "0-30 days" : days <= 60 ? "31-60 days" : days <= 90 ? "61-90 days" : "> 90 days";
                return new object?[]
                {
                    c.ContractNumber, c.Title, suppliers.GetValueOrDefault(c.SupplierId), Ref(projects, c.ProjectId), c.Source.ToString(), c.Status.ToString(),
                    c.OriginalValue, c.ApprovedVariations, c.RevisedValue, c.StartDate, c.OriginalEndDate, c.CurrentEndDate, days, band,
                    c.ContractManagerName, c.PerformanceRating
                };
            }).ToList();
        return (columns, rows);
    }

    // RPT-006
    private async Task<(string[], List<object?[]>)> VariationsAsync(ReportFilter filter, DataScope scope, Dictionary<Guid, ProjectInfo> projects, CancellationToken ct)
    {
        var contracts = await _db.Contracts.AsNoTracking().InScope(scope, c => c.ProjectId).ToDictionaryAsync(c => c.Id, ct);
        var contractIds = contracts.Keys.ToList();
        var variations = await _db.ContractVariations.AsNoTracking().Where(v => contractIds.Contains(v.ContractId)).ToListAsync(ct);
        var suppliers = await SupplierNamesAsync(contracts.Values.Select(c => c.SupplierId), ct);
        var columns = new[]
        {
            "Variation", "Contract", "Supplier", "Project", "Type", "Extension", "Description", "Reason", "Amount", "Days",
            "Value before", "Value after", "End before", "End after", "Cumulative variation % of original", "Status", "Decided by", "Decided"
        };
        var rows = variations.Where(v => StatusMatches(filter, v.Status.ToString()) && InPeriod(filter, DateOnly.FromDateTime(v.CreatedAtUtc)))
            .OrderBy(v => v.CreatedAtUtc)
            .Select(v =>
            {
                var c = contracts[v.ContractId];
                var cumulative = variations.Where(x => x.ContractId == v.ContractId && x.Status == ApprovalState.Approved && x.CreatedAtUtc <= v.CreatedAtUtc)
                    .Sum(x => x.Amount);
                return new object?[]
                {
                    v.Number, c.ContractNumber, suppliers.GetValueOrDefault(c.SupplierId), Ref(projects, c.ProjectId), v.Type.ToString(), v.IsExtension,
                    v.Description, v.Reason, v.Amount, v.Days, v.ValueBefore, v.ValueAfter, v.EndDateBefore, v.EndDateAfter,
                    c.OriginalValue == 0 ? 0m : Math.Round(cumulative / c.OriginalValue * 100m, 1), v.Status.ToString(), v.DecidedBy, v.DecidedAtUtc
                };
            }).ToList();
        return (columns, rows);
    }

    // RPT-007
    private async Task<(string[], List<object?[]>)> ProjectFinancialsAsync(ReportFilter filter, DataScope scope, Dictionary<Guid, ProjectInfo> projects, CancellationToken ct)
    {
        var fy = filter.FinancialYear;
        var lines = await _db.BudgetLines.AsNoTracking().InScope(scope, b => b.ProjectId).Where(b => fy == null || b.FinancialYear == fy)
            .Select(b => new { b.ProjectId, b.OriginalAmount, b.RevisedAmount, b.ForecastAmount }).ToListAsync(ct);
        var commitments = await _db.Commitments.AsNoTracking().InScope(scope, c => c.ProjectId).Where(c => !c.IsReleased && (fy == null || c.FinancialYear == fy))
            .Select(c => new { c.ProjectId, c.Amount }).ToListAsync(ct);
        var actuals = await _db.Expenditures.AsNoTracking().InScope(scope, e => e.ProjectId).Where(e => fy == null || e.FinancialYear == fy)
            .Select(e => new { e.ProjectId, e.Amount }).ToListAsync(ct);
        var accruals = await _db.Accruals.AsNoTracking().InScope(scope, a => a.ProjectId).Where(a => !a.IsReversed)
            .Select(a => new { a.ProjectId, a.Amount }).ToListAsync(ct);
        var forecasts = (await _db.CostForecasts.AsNoTracking().InScope(scope, f => f.ProjectId).ToListAsync(ct))
            .GroupBy(f => f.ProjectId).ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.RecordedAtUtc).First());

        var columns = new[]
        {
            "Project", "Name", "Programme", "Health", "Original budget", "Revised budget", "Committed", "Actual", "Accrued", "Available",
            "Spend %", "Estimate at completion", "Forecast variance", "Variance %", "Forecast commentary"
        };
        var rows = projects.Values.OrderBy(p => p.Reference).Select(p =>
        {
            var original = lines.Where(l => l.ProjectId == p.Id).Sum(l => l.OriginalAmount);
            var revised = lines.Where(l => l.ProjectId == p.Id).Sum(l => l.RevisedAmount);
            if (revised == 0 && fy is null) revised = p.Project.ApprovedBudget;
            var committed = commitments.Where(c => c.ProjectId == p.Id).Sum(c => c.Amount);
            var actual = actuals.Where(a => a.ProjectId == p.Id).Sum(a => a.Amount);
            var accrued = accruals.Where(a => a.ProjectId == p.Id).Sum(a => a.Amount);
            forecasts.TryGetValue(p.Id, out var forecast);
            var forecastLines = lines.Where(l => l.ProjectId == p.Id).Sum(l => l.ForecastAmount);
            var eac = forecast?.EstimateAtCompletion ?? (forecastLines > 0 ? forecastLines : revised);
            return new object?[]
            {
                p.Reference, p.Project.Name, p.ProgrammeName, p.Project.Health.ToString(), original, revised, committed, actual, accrued,
                revised - committed - actual, revised == 0 ? 0m : Math.Round(actual / revised * 100m, 1), eac, eac - revised,
                revised == 0 ? 0m : Math.Round((eac - revised) / revised * 100m, 1), forecast?.Commentary
            };
        }).ToList();
        return (columns, rows);
    }

    // RPT-008
    private async Task<(string[], List<object?[]>)> InvoicesAsync(ReportFilter filter, DataScope scope, Dictionary<Guid, ProjectInfo> projects, CancellationToken ct)
    {
        var invoices = await _db.Invoices.AsNoTracking().InScope(scope, i => i.ProjectId).ToListAsync(ct);
        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var payments = await _db.Payments.AsNoTracking().Where(p => invoiceIds.Contains(p.InvoiceId)).ToListAsync(ct);
        var contractNumbers = await _db.Contracts.AsNoTracking().InScope(scope, c => c.ProjectId)
            .Select(c => new { c.Id, c.ContractNumber }).ToDictionaryAsync(c => c.Id, c => c.ContractNumber, ct);
        var suppliers = await SupplierNamesAsync(invoices.Select(i => i.SupplierId), ct);
        var today = _clock.Today;
        var columns = new[]
        {
            "Invoice", "Supplier invoice no.", "Supplier", "Contract", "Project", "Invoice date", "Received", "Amount", "VAT", "Status",
            "Age (days)", "Over 30 days unpaid", "Potential duplicate", "Certified by", "Certified", "Paid amount", "Payment date", "ERP reference"
        };
        var rows = invoices.Where(i => StatusMatches(filter, i.Status.ToString()) && InPeriod(filter, i.ReceivedDate))
            .OrderBy(i => i.ReceivedDate)
            .Select(i =>
            {
                var paid = payments.Where(p => p.InvoiceId == i.Id && p.Status == PaymentStatus.Paid).ToList();
                var age = today.DayNumber - i.ReceivedDate.DayNumber;
                return new object?[]
                {
                    i.Number, i.SupplierInvoiceNumber, suppliers.GetValueOrDefault(i.SupplierId), contractNumbers.GetValueOrDefault(i.ContractId),
                    Ref(projects, i.ProjectId), i.InvoiceDate, i.ReceivedDate, i.Amount, i.VatAmount, i.Status.ToString(), age,
                    paid.Count == 0 && age > 30 && i.Status is not (InvoiceStatus.Rejected or InvoiceStatus.Cancelled), i.PotentialDuplicate,
                    i.CertifiedBy, i.CertifiedAtUtc, paid.Sum(p => p.Amount), paid.Select(p => (DateOnly?)p.PaymentDate).Max(),
                    string.Join(", ", paid.Select(p => p.ErpReference))
                };
            }).ToList();
        return (columns, rows);
    }

    // RPT-009
    private async Task<(string[], List<object?[]>)> SupplierPerformanceAsync(ReportFilter filter, DataScope scope, CancellationToken ct)
    {
        var contracts = await _db.Contracts.AsNoTracking().InScope(scope, c => c.ProjectId).ToListAsync(ct);
        var contractIds = contracts.Select(c => c.Id).ToList();
        var reviews = await _db.ContractPerformanceReviews.AsNoTracking().Where(r => contractIds.Contains(r.ContractId)).ToListAsync(ct);
        var deliverables = await _db.Deliverables.AsNoTracking().Where(d => d.ContractId != null && contractIds.Contains(d.ContractId.Value)).ToListAsync(ct);
        var breaches = await _db.ContractBreaches.AsNoTracking().Where(b => contractIds.Contains(b.ContractId)).ToListAsync(ct);
        var supplierIds = contracts.Select(c => c.SupplierId).Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToListAsync(ct);
        var today = _clock.Today;

        var columns = new[]
        {
            "Supplier", "Supplier no.", "Status", "B-BBEE level", "Contracts", "Active contracts", "Contract value", "Reviews",
            "Average overall score", "Latest rating", "Deliverables accepted", "Deliverables rejected", "Deliverables overdue", "Open breaches", "Total breaches"
        };
        var rows = suppliers.OrderBy(s => s.LegalName).Select(s =>
        {
            var own = contracts.Where(c => c.SupplierId == s.Id).ToList();
            var ownIds = own.Select(c => c.Id).ToHashSet();
            var ownReviews = reviews.Where(r => ownIds.Contains(r.ContractId))
                .Where(r => filter.From is null || r.ReviewDate >= filter.From).Where(r => filter.To is null || r.ReviewDate <= filter.To).ToList();
            var ownDeliverables = deliverables.Where(d => d.ContractId is { } cid && ownIds.Contains(cid)).ToList();
            var ownBreaches = breaches.Where(b => ownIds.Contains(b.ContractId)).ToList();
            return new object?[]
            {
                s.LegalName, s.SupplierNumber, s.Status.ToString(), s.BbbeeLevel, own.Count, own.Count(c => c.Status == ContractStatus.Active),
                own.Sum(c => c.RevisedValue), ownReviews.Count,
                ownReviews.Count == 0 ? null : Math.Round(ownReviews.Average(r => r.OverallScore), 1),
                ownReviews.OrderByDescending(r => r.ReviewDate).FirstOrDefault()?.Rating,
                ownDeliverables.Count(d => d.AcceptanceStatus == AcceptanceStatus.Accepted),
                ownDeliverables.Count(d => d.AcceptanceStatus == AcceptanceStatus.Rejected),
                ownDeliverables.Count(d => d.AcceptanceStatus != AcceptanceStatus.Accepted && d.DueDate < today),
                ownBreaches.Count(b => b.Status != BreachStatus.Closed && b.Status != BreachStatus.Remedied), ownBreaches.Count
            };
        }).ToList();
        return (columns, rows);
    }

    // RPT-010
    private async Task<(string[], List<object?[]>)> MonitoringAsync(ReportFilter filter, DataScope scope, Dictionary<Guid, ProjectInfo> projects, CancellationToken ct)
    {
        var today = _clock.Today;
        var visits = await _db.MonitoringVisits.AsNoTracking().InScope(scope, v => v.ProjectId).ToListAsync(ct);
        visits = visits.Where(v => InPeriod(filter, v.VisitDate ?? v.ScheduledDate)).ToList();
        var findings = await _db.Findings.AsNoTracking().InScope(scope, f => f.ProjectId).ToListAsync(ct);
        var actions = await _db.CorrectiveActions.AsNoTracking().InScopeNullable(scope, a => a.ProjectId).ToListAsync(ct);

        var columns = new[]
        {
            "Project", "Name", "Visits scheduled", "Visits completed", "Visits overdue", "Findings open", "Findings critical/high open",
            "Findings closed", "Actions open", "Actions overdue", "Actions completed", "Oldest overdue action due"
        };
        var rows = projects.Values.OrderBy(p => p.Reference).Select(p =>
        {
            var pv = visits.Where(v => v.ProjectId == p.Id).ToList();
            var pf = findings.Where(f => f.ProjectId == p.Id).ToList();
            var pa = actions.Where(a => a.ProjectId == p.Id).ToList();
            var openActions = pa.Where(a => a.Status is ActionStatus.Open or ActionStatus.InProgress).ToList();
            var overdue = openActions.Where(a => a.DueDate < today).ToList();
            return new object?[]
            {
                p.Reference, p.Project.Name, pv.Count(v => v.Status == VisitStatus.Scheduled), pv.Count(v => v.Status == VisitStatus.Completed),
                pv.Count(v => v.Status == VisitStatus.Scheduled && v.ScheduledDate < today), pf.Count(f => f.Status != FindingStatus.Closed),
                pf.Count(f => f.Status != FindingStatus.Closed && f.Severity >= Severity.High), pf.Count(f => f.Status == FindingStatus.Closed),
                openActions.Count, overdue.Count, pa.Count(a => a.Status == ActionStatus.Completed),
                overdue.Count == 0 ? null : overdue.Min(a => a.DueDate)
            };
        })
        .Where(r => (int)r[2]! + (int)r[3]! + (int)r[5]! + (int)r[7]! + (int)r[8]! + (int)r[10]! > 0 || filter.ProjectId is not null)
        .ToList();
        return (columns, rows);
    }

    // RPT-011
    private async Task<(string[], List<object?[]>)> RisksAsync(ReportFilter filter, DataScope scope, Dictionary<Guid, ProjectInfo> projects, CancellationToken ct)
    {
        var today = _clock.Today;
        var query = _db.Risks.AsNoTracking().Where(r => r.Status != RiskStatus.Closed);
        if (!scope.All)
        {
            var ids = scope.ProjectIdList.Select(g => (Guid?)g).ToList();
            query = query.Where(r => r.ProjectId == null || ids.Contains(r.ProjectId));
        }
        var risks = await query.ToListAsync(ct);
        if (!scope.All && filter.HasProjectFilter) risks = risks.Where(r => r.ProjectId is not null).ToList();
        var riskIds = risks.Select(r => r.Id).ToList();
        var treatments = await _db.RiskTreatments.AsNoTracking().Where(t => riskIds.Contains(t.RiskId)).ToListAsync(ct);

        var columns = new[]
        {
            "Risk", "Title", "Project", "Category", "Inherent L x I", "Inherent score", "Inherent rating", "Residual L x I", "Residual score",
            "Residual rating", "Owner", "Review date", "Review overdue", "Open treatments", "Overdue treatments", "Oldest overdue treatment", "Status"
        };
        var rows = risks.Where(r => StatusMatches(filter, r.Status.ToString()) || StatusMatches(filter, r.ResidualRating))
            .OrderByDescending(r => r.ResidualScore).ThenBy(r => r.Number)
            .Select(r =>
            {
                var open = treatments.Where(t => t.RiskId == r.Id && t.Status is ActionStatus.Open or ActionStatus.InProgress).ToList();
                var overdue = open.Where(t => t.DueDate < today).ToList();
                return new object?[]
                {
                    r.Number, r.Title, Ref(projects, r.ProjectId), r.Category, $"{r.InherentLikelihood} x {r.InherentImpact}", r.InherentScore,
                    r.InherentRating, $"{r.ResidualLikelihood} x {r.ResidualImpact}", r.ResidualScore, r.ResidualRating, r.OwnerName, r.ReviewDate,
                    r.ReviewDate < today, open.Count, overdue.Count, overdue.Count == 0 ? null : overdue.Min(t => t.DueDate), r.Status.ToString()
                };
            }).ToList();
        return (columns, rows);
    }

    // RPT-012
    private async Task<(string[], List<object?[]>)> AuditFindingsAsync(ReportFilter filter, DataScope scope, Dictionary<Guid, ProjectInfo> projects, CancellationToken ct)
    {
        var today = _clock.Today;
        var query = _db.AuditFindings.AsNoTracking();
        if (!scope.All)
        {
            var ids = scope.ProjectIdList.Select(g => (Guid?)g).ToList();
            query = filter.HasProjectFilter ? query.Where(f => ids.Contains(f.ProjectId)) : query.Where(f => f.ProjectId == null || ids.Contains(f.ProjectId));
        }
        var findings = await query.ToListAsync(ct);
        var findingIds = findings.Select(f => f.Id).ToList();
        var actions = await _db.CorrectiveActions.AsNoTracking()
            .Where(a => a.ParentType == ParentTypes.AuditFinding && findingIds.Contains(a.ParentId)).ToListAsync(ct);

        var columns = new[]
        {
            "Finding", "Title", "Source", "Audit reference", "Process", "Project", "Rating", "Recommendation", "Management response",
            "Action owner", "Due date", "Days overdue", "Actions open", "Actions completed", "Status", "Closed"
        };
        var rows = findings.Where(f => StatusMatches(filter, f.Status.ToString()) && InPeriod(filter, f.DueDate))
            .OrderByDescending(f => f.Rating).ThenBy(f => f.DueDate)
            .Select(f =>
            {
                var own = actions.Where(a => a.ParentId == f.Id).ToList();
                var overdueDays = f.Status != FindingStatus.Closed && f.DueDate < today ? today.DayNumber - f.DueDate.DayNumber : 0;
                return new object?[]
                {
                    f.Number, f.Title, f.Source.ToString(), f.AuditReference, f.Process, Ref(projects, f.ProjectId), f.Rating.ToString(), f.Recommendation,
                    f.ManagementResponse, f.ActionOwnerName, f.DueDate, overdueDays, own.Count(a => a.Status is ActionStatus.Open or ActionStatus.InProgress),
                    own.Count(a => a.Status == ActionStatus.Completed), f.Status.ToString(), f.ClosedAtUtc
                };
            }).ToList();
        return (columns, rows);
    }

    // RPT-013
    private async Task<(string[], List<object?[]>)> EvidenceVerificationAsync(ReportFilter filter, DataScope scope, Dictionary<Guid, ProjectInfo> projects, CancellationToken ct)
    {
        var results = await _db.PerformanceResults.AsNoTracking().InScope(scope, r => r.ProjectId)
            .Where(r => (filter.FinancialYear == null || r.FinancialYear == filter.FinancialYear) && (filter.Quarter == null || r.Quarter == filter.Quarter))
            .ToListAsync(ct);
        var resultIds = results.Select(r => r.Id).ToList();
        var evidence = await (from e in _db.Evidence.AsNoTracking()
                              join d in _db.Documents.AsNoTracking() on e.DocumentId equals d.Id
                              where e.ParentType == ParentTypes.PerformanceResult && resultIds.Contains(e.ParentId)
                              select new { e.ParentId, e.EvidenceType, e.VerificationStatus, e.VerifiedBy, d.Title, d.DocumentVersion }).ToListAsync(ct);
        var indicatorIds = results.Select(r => r.IndicatorId).Distinct().ToList();
        var indicators = await _db.AppIndicators.AsNoTracking().Where(i => indicatorIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);

        var columns = new[]
        {
            "Indicator", "Indicator name", "Project", "Financial year", "Quarter", "Value", "Result status", "Evidence items", "Verified evidence",
            "Evidence (type: document v#)", "Required evidence types", "Missing evidence types", "Verified by", "Verified", "Verification comment", "Traceable"
        };
        var rows = results.Where(r => StatusMatches(filter, r.Status.ToString()))
            .OrderBy(r => indicators.GetValueOrDefault(r.IndicatorId)?.Code).ThenBy(r => r.Quarter)
            .Select(r =>
            {
                indicators.TryGetValue(r.IndicatorId, out var indicator);
                var own = evidence.Where(e => e.ParentId == r.Id).ToList();
                var verified = own.Where(e => e.VerificationStatus == EvidenceStatus.Verified).ToList();
                var required = indicator?.RequiredEvidenceTypeList() ?? Array.Empty<string>();
                var missing = required.Where(t => !verified.Any(v => string.Equals(v.EvidenceType, t, StringComparison.OrdinalIgnoreCase))).ToList();
                return new object?[]
                {
                    indicator?.Code, indicator?.Name, Ref(projects, r.ProjectId), r.FinancialYear, r.Quarter, r.Value, r.Status.ToString(), own.Count,
                    verified.Count, string.Join("; ", own.Select(e => $"{e.EvidenceType}: {e.Title} v{e.DocumentVersion} ({e.VerificationStatus})")),
                    string.Join(", ", required), string.Join(", ", missing), r.VerifiedBy, r.VerifiedAtUtc, r.VerificationComment,
                    r.Status == ResultStatus.Verified && verified.Count > 0 && missing.Count == 0
                };
            }).ToList();
        return (columns, rows);
    }

    // RPT-014
    private async Task<(string[], List<object?[]>)> CloseOutAsync(ReportFilter filter, DataScope scope, Dictionary<Guid, ProjectInfo> projects, CancellationToken ct)
    {
        var closures = await _db.ProjectClosures.AsNoTracking().InScope(scope, c => c.ProjectId).ToListAsync(ct);
        var reviews = await _db.BenefitReviews.AsNoTracking().InScope(scope, b => b.ProjectId).ToListAsync(ct);
        var closing = projects.Values.Where(p => p.Project.Status is ProjectStatus.Closing or ProjectStatus.Closed
                                                 || closures.Any(c => c.ProjectId == p.Id) || reviews.Any(r => r.ProjectId == p.Id));
        var columns = new[]
        {
            "Project", "Name", "Status", "Stage", "Actual end", "Closure status", "Closure decided", "Financial reconciliation", "Documentation complete",
            "Lessons learned", "Benefit reviews scheduled", "Benefit reviews completed", "Expected benefit value", "Actual benefit value", "Benefit realisation %"
        };
        var rows = closing.OrderBy(p => p.Reference).Select(p =>
        {
            var closure = closures.Where(c => c.ProjectId == p.Id).OrderByDescending(c => c.CreatedAtUtc).FirstOrDefault();
            var own = reviews.Where(r => r.ProjectId == p.Id).ToList();
            var completed = own.Where(r => r.Status == BenefitReviewStatus.Completed).ToList();
            var expected = completed.Sum(r => r.ExpectedValue ?? 0m);
            var actual = completed.Sum(r => r.ActualValue ?? 0m);
            return new object?[]
            {
                p.Reference, p.Project.Name, p.Project.Status.ToString(), p.Project.Stage.ToString(), p.Project.ActualEnd, closure?.Status.ToString() ?? "Not started",
                closure?.DecidedAtUtc, closure?.FinancialReconciliationConfirmed, closure?.DocumentationComplete, closure?.LessonsLearned,
                own.Count(r => r.Status == BenefitReviewStatus.Scheduled), completed.Count, expected, actual,
                expected == 0 ? null : Math.Round(actual / expected * 100m, 1)
            };
        }).ToList();
        return (columns, rows);
    }

    // RPT-015
    private async Task<(string[], List<object?[]>)> DataQualityAsync(ReportFilter filter, CancellationToken ct)
    {
        var issues = await _db.DataQualityIssues.AsNoTracking()
            .Where(i => i.Status == Domain.Reporting.DataQualityStatus.Open).ToListAsync(ct);
        var errors = await _db.ErpInterfaceMessages.AsNoTracking().Where(m => m.Status == InterfaceStatus.Error).ToListAsync(ct);
        var unbalanced = await _db.ErpReconciliations.AsNoTracking().Where(r => !r.Balanced).ToListAsync(ct);

        var columns = new[] { "Category", "Rule / type", "Reference", "Description", "Severity", "Owner", "Detected", "Status" };
        var rows = new List<object?[]>();
        rows.AddRange(issues.OrderByDescending(i => i.Severity).ThenBy(i => i.DetectedAtUtc).Select(i => new object?[]
        {
            "Data quality", i.RuleCode, i.EntityReference, i.Description, i.Severity.ToString(), i.OwnerName, i.DetectedAtUtc, i.Status.ToString()
        }));
        rows.AddRange(errors.OrderBy(m => m.CreatedAtUtc).Select(m => new object?[]
        {
            "Interface error", $"{m.Direction} {m.MessageType}", m.ExternalReference, m.Error, "High", "Finance / ICT", m.CreatedAtUtc,
            $"Error after {m.Attempts} attempt(s)"
        }));
        rows.AddRange(unbalanced.OrderBy(r => r.RunAtUtc).Select(r => new object?[]
        {
            "Unreconciled batch", r.MessageType, r.BatchId.ToString(),
            $"Expected {r.ExpectedCount} / {r.ExpectedTotal:0.00}, processed {r.ProcessedCount} / {r.ProcessedTotal:0.00}, errors {r.ErrorCount}",
            "High", "Finance", r.RunAtUtc, "Unbalanced"
        }));
        return (columns, rows.Where(r => StatusMatches(filter, (string)r[0]!) || string.IsNullOrWhiteSpace(filter.Status)).ToList());
    }

    private async Task<Dictionary<Guid, string>> SupplierNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await _db.Suppliers.AsNoTracking().Where(s => list.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.LegalName, ct);
    }
}
