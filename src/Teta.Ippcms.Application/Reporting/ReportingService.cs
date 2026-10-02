using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Strategy;
using Teta.Ippcms.Domain.Admin;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Execution;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Monitoring;
using Teta.Ippcms.Domain.Projects;
using Teta.Ippcms.Domain.Reporting;
using Teta.Ippcms.Domain.Scm;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Strategy;
using Teta.Ippcms.Domain.Suppliers;

namespace Teta.Ippcms.Application.Reporting;

// ---------- DTOs ----------
public sealed record KpiDto(string Code, string Label, decimal Value, string? Unit, string Status, string DrillLink);
public sealed record HealthBucket(string Health, int Count);
public sealed record ProgrammeRollupDto(Guid ProgrammeId, string Name, string PortfolioName, int Projects, int Red, int Amber, int Green, decimal Budget,
    decimal Actual);
public sealed record ExceptionItemDto(string Category, string Reference, string Description, string Severity, Guid? ProjectId, string? ProjectReference,
    DateOnly? DueDate, int? DaysOverdue, string Link);
public sealed record ExecutiveDashboardDto(string FinancialYear, IReadOnlyList<KpiDto> Kpis, IReadOnlyList<HealthBucket> Health,
    IReadOnlyList<ProgrammeRollupDto> Programmes, IReadOnlyList<IndicatorPerformanceDto> AppPerformance, IReadOnlyList<ExceptionItemDto> TopExceptions);

public sealed record BoardPackDto(Guid Id, string Number, string Period, int PackVersion, string Title, string Status, DateTime GeneratedAtUtc,
    string? GeneratedBy, DateTime? ApprovedAtUtc, string? ApprovedBy);
public sealed record BoardPackDetailDto(BoardPackDto Pack, JsonElement Dataset);
public sealed record GenerateBoardPackRequest(string Period, string Title, string? FinancialYear);

/// <summary>A frozen columnar table (as produced by <see cref="ReportTable"/>) stored inside a Board Pack's dataset JSON.</summary>
public sealed record BoardPackTableDto(IReadOnlyList<string> Columns, IReadOnlyList<string[]> Rows);

/// <summary>Shape of <see cref="BoardReportPack.DatasetJson"/>, matching the anonymous object built in <see cref="ReportingService.GenerateBoardPackAsync"/>.</summary>
public sealed record BoardPackDatasetDto(string Period, string FinancialYear, DateTime GeneratedAtUtc, string? GeneratedBy,
    IReadOnlyList<KpiDto> Kpis, IReadOnlyList<HealthBucket> Health, IReadOnlyList<ProgrammeRollupDto> Programmes,
    IReadOnlyList<IndicatorPerformanceDto> AppPerformance, IReadOnlyList<ExceptionItemDto> Exceptions,
    BoardPackTableDto PortfolioHealth, BoardPackTableDto EvidenceVerification);

public sealed record ReportScheduleDto(Guid Id, string ReportCode, string ReportName, string Format, string Frequency, string Recipients, bool IsEnabled,
    Guid OwnerUserId, DateTime? LastRunAtUtc, DateTime? NextRunAtUtc, string? LastRunStatus, long Version);
public sealed record SaveScheduleRequest(string ReportCode, string Format, string Frequency, string Recipients, bool IsEnabled, string? FinancialYear,
    Guid? ProgrammeId, Guid? ProjectId);

public sealed record GeoAreaDto(string Province, string? District, string? Municipality, int Projects, decimal Budget, int? Beneficiaries, bool Suppressed);
public sealed record GeoViewDto(string Level, int MinimumGroupSize, IReadOnlyList<GeoAreaDto> Areas, int ProjectsWithoutLocation);

public sealed record DataQualityIssueDto(Guid Id, string RuleCode, string Description, string EntityType, Guid? EntityId, string? EntityReference,
    string Severity, string Status, Guid? OwnerUserId, string? OwnerName, string? Resolution, DateTime DetectedAtUtc, DateTime? ResolvedAtUtc, long Version);
public sealed record RuleCount(string RuleCode, int Open);
public sealed record DataQualityDashboardDto(int Open, int ResolvedLast30Days, IReadOnlyList<RuleCount> ByRule, int InterfaceErrors, int UnbalancedBatches,
    IReadOnlyList<DataQualityIssueDto> Issues);
public sealed record ResolveIssueRequest(DataQualityStatus Status, string Resolution);

public interface IReportingService
{
    IReadOnlyList<ReportDefinition> Catalogue();
    Task<ReportTable> RunAsync(string code, ReportFilter filter, CancellationToken ct);
    Task<ExportedFile> ExportAsync(string code, ReportFilter filter, string format, CancellationToken ct);

    Task<ExecutiveDashboardDto> ExecutiveDashboardAsync(string? financialYear, CancellationToken ct);
    Task<IReadOnlyList<ExceptionItemDto>> ExceptionsAsync(ReportFilter filter, CancellationToken ct);
    Task<ExportedFile> ExportExceptionsAsync(ReportFilter filter, string format, CancellationToken ct);

    Task<IReadOnlyList<BoardPackDto>> ListBoardPacksAsync(string? period, CancellationToken ct);
    Task<BoardPackDetailDto> GetBoardPackAsync(Guid id, CancellationToken ct);
    Task<BoardPackDto> GenerateBoardPackAsync(GenerateBoardPackRequest request, CancellationToken ct);
    Task<BoardPackDto> ApproveBoardPackAsync(Guid id, CancellationToken ct);
    Task<ExportedFile> ExportBoardPackAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<ReportScheduleDto>> ListSchedulesAsync(CancellationToken ct);
    Task<ReportScheduleDto> SaveScheduleAsync(Guid? id, SaveScheduleRequest request, CancellationToken ct);
    Task<int> RunDueSchedulesAsync(CancellationToken ct);

    Task<ReportTable> AnalyticsAsync(ReportFilter filter, CancellationToken ct);
    Task<GeoViewDto> GeographicAsync(string level, ReportFilter filter, CancellationToken ct);

    Task<DataQualityDashboardDto> DataQualityAsync(string? status, string? ruleCode, CancellationToken ct);
    Task<DataQualityIssueDto> ResolveIssueAsync(Guid id, ResolveIssueRequest request, CancellationToken ct);
    Task<int> ScanDataQualityAsync(CancellationToken ct);
}

/// <summary>Reporting, analytics and executive dashboards (SRS §5.10, §11).</summary>
public sealed class ReportingService : IReportingService
{
    private static readonly string[] Frequencies = { "Daily", "Weekly", "Monthly", "Quarterly" };

    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAccessScope _scope;
    private readonly ISettings _settings;
    private readonly IStrategyService _strategy;
    private readonly INumberGenerator _numbers;
    private readonly IAuditWriter _audit;
    private readonly ReportBuilder _builder;

    public ReportingService(ITetaDbContext db, ICurrentUser user, IClock clock, IAccessScope scope, ISettings settings, IStrategyService strategy,
        INumberGenerator numbers, IAuditWriter audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _scope = scope;
        _settings = settings;
        _strategy = strategy;
        _numbers = numbers;
        _audit = audit;
        _builder = new ReportBuilder(db, clock, settings, strategy);
    }

    public IReadOnlyList<ReportDefinition> Catalogue() =>
        ReportCatalogue.All.Where(r => _user.HasPermission(r.Permission)).ToList();

    public async Task<ReportTable> RunAsync(string code, ReportFilter filter, CancellationToken ct)
    {
        var definition = ReportCatalogue.Get(code);
        if (!_user.HasPermission(definition.Permission)) throw new ForbiddenException($"You are not authorised to run {definition.Code}.");
        Validate(filter);
        return await _builder.BuildAsync(definition.Code, filter, await _scope.GetAsync(ct), ct);
    }

    /// <summary>Authorised export; every export event is audited with report, format, filters and row count (FR-REP-008).</summary>
    public async Task<ExportedFile> ExportAsync(string code, ReportFilter filter, string format, CancellationToken ct)
    {
        var table = await RunAsync(code, filter, ct);
        var file = ReportExport.Export(table, format);
        await LogExportAsync(table, format, ct);
        return file;
    }

    private async Task LogExportAsync(ReportTable table, string format, CancellationToken ct)
    {
        _audit.Write("Reporting", "Report", table.Code, "Export",
            new { table.Code, Format = format.ToLowerInvariant(), table.RowCount, Filters = table.Filters.Where(f => f.Value != null).ToDictionary(f => f.Key, f => f.Value) });
        await _db.SaveChangesAsync(ct);
    }

    private static void Validate(ReportFilter filter)
    {
        new Validator()
            .Must(filter.FinancialYear is null || Fy.IsValid(filter.FinancialYear), "financialYear", "Use the format 2026/27.")
            .Must(filter.Quarter is null or >= 1 and <= 4, "quarter", "Quarter must be 1-4.")
            .DateOrder("from", filter.From, "to", filter.To)
            .ThrowIfInvalid();
    }

    // ---------------- Executive dashboard (FR-REP-002) ----------------
    public async Task<ExecutiveDashboardDto> ExecutiveDashboardAsync(string? financialYear, CancellationToken ct)
    {
        var fy = financialYear ?? await _settings.GetAsync(SettingKeys.CurrentFinancialYear, ct);
        Validate(new ReportFilter(fy));
        var scope = await _scope.GetAsync(ct);
        var (effective, projects) = await _builder.ResolveAsync(new ReportFilter(), scope, ct);
        var today = _clock.Today;

        var active = projects.Values.Where(p => p.Project.Status is ProjectStatus.InExecution or ProjectStatus.Approved or ProjectStatus.Closing).ToList();
        var budget = (await _db.BudgetLines.AsNoTracking().InScope(effective, b => b.ProjectId).Where(b => b.FinancialYear == fy)
            .Select(b => new { b.ProjectId, b.RevisedAmount }).ToListAsync(ct));
        var actual = (await _db.Expenditures.AsNoTracking().InScope(effective, e => e.ProjectId).Where(e => e.FinancialYear == fy)
            .Select(e => new { e.ProjectId, e.Amount }).ToListAsync(ct));
        var committed = (await _db.Commitments.AsNoTracking().InScope(effective, c => c.ProjectId).Where(c => c.FinancialYear == fy && !c.IsReleased)
            .Select(c => new { c.Amount }).ToListAsync(ct)).Sum(c => c.Amount);
        var totalBudget = budget.Sum(b => b.RevisedAmount);
        var totalActual = actual.Sum(a => a.Amount);

        var openProcurements = await _db.Procurements.AsNoTracking().InScope(effective, p => p.ProjectId)
            .Where(p => p.Status != ProcurementStatus.Awarded && p.Status != ProcurementStatus.Cancelled).Select(p => p.CreatedAtUtc).ToListAsync(ct);
        var ageing = await _settings.GetIntAsync(SettingKeys.ExceptionProcurementAgeingDays, ct);
        var aged = openProcurements.Count(c => (_clock.UtcNow - c).TotalDays > ageing);

        var contracts = await _db.Contracts.AsNoTracking().InScope(effective, c => c.ProjectId)
            .Where(c => c.Status == ContractStatus.Active).Select(c => new { c.CurrentEndDate, c.OriginalValue, c.ApprovedVariations }).ToListAsync(ct);
        var expiring = contracts.Count(c => c.CurrentEndDate >= today && c.CurrentEndDate.DayNumber - today.DayNumber <= 90);

        var pendingInvoices = await _db.Invoices.AsNoTracking().InScope(effective, i => i.ProjectId)
            .CountAsync(i => i.Status == InvoiceStatus.PendingCertification || i.Status == InvoiceStatus.Registered, ct);

        var riskQuery = _db.Risks.AsNoTracking().Where(r => r.Status != RiskStatus.Closed);
        if (!effective.All)
        {
            var ids = effective.ProjectIdList.Select(g => (Guid?)g).ToList();
            riskQuery = riskQuery.Where(r => r.ProjectId == null || ids.Contains(r.ProjectId));
        }
        var ratings = await riskQuery.Select(r => r.ResidualRating).ToListAsync(ct);

        var overdueActions = await _db.CorrectiveActions.AsNoTracking().InScopeNullable(effective, a => a.ProjectId)
            .CountAsync(a => (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress) && a.DueDate < today, ct);
        var openAuditFindings = await _db.AuditFindings.AsNoTracking().CountAsync(f => f.Status != FindingStatus.Closed, ct);

        var performance = await _strategy.GetPerformanceAsync(fy, null, null, ct);
        var withTargets = performance.Where(p => p.Status != "NoTarget").ToList();
        var onTrack = withTargets.Count(p => p.Status is "Achieved" or "OnTrack");

        string Rag(decimal value, decimal amber, decimal red, bool higherIsWorse = true) =>
            higherIsWorse ? value >= red ? "Red" : value >= amber ? "Amber" : "Green"
                          : value <= red ? "Red" : value <= amber ? "Amber" : "Green";

        var redCount = active.Count(p => p.Project.Health == HealthStatus.Red);
        var spendPct = totalBudget == 0 ? 0 : Math.Round(totalActual / totalBudget * 100m, 1);
        var appPct = withTargets.Count == 0 ? 0 : Math.Round(onTrack * 100m / withTargets.Count, 1);
        var critical = ratings.Count(r => r == "Critical");

        var kpis = new List<KpiDto>
        {
            new("ACTIVE_PROJECTS", "Active projects", active.Count, null, "Info", "/projects?status=InExecution"),
            new("RED_PROJECTS", "Projects in red", redCount, null, redCount > 0 ? "Red" : "Green", "/projects?health=Red"),
            new("APP_ON_TRACK", "APP indicators on track", appPct, "%", Rag(appPct, 80, 60, higherIsWorse: false), $"/strategy/performance?fy={Uri.EscapeDataString(fy)}"),
            new("BUDGET", "Revised budget", totalBudget, "ZAR", "Info", $"/finance?fy={Uri.EscapeDataString(fy)}"),
            new("COMMITTED", "Committed", committed, "ZAR", "Info", $"/finance?fy={Uri.EscapeDataString(fy)}"),
            new("SPEND", "Actual expenditure", totalActual, "ZAR", "Info", $"/finance?fy={Uri.EscapeDataString(fy)}"),
            new("SPEND_PCT", "Budget spent", spendPct, "%", "Info", $"/reports/RPT-007?fy={Uri.EscapeDataString(fy)}"),
            new("OPEN_PROCUREMENTS", "Procurements in progress", openProcurements.Count, null, "Info", "/procurement"),
            new("AGED_PROCUREMENTS", $"Procurements older than {ageing} days", aged, null, aged > 0 ? "Amber" : "Green", "/reports/RPT-004"),
            new("CONTRACTS_EXPIRING", "Contracts expiring in 90 days", expiring, null, expiring > 0 ? "Amber" : "Green", "/contracts?expiring=true"),
            new("INVOICES_PENDING", "Invoices awaiting certification", pendingInvoices, null, pendingInvoices > 0 ? "Amber" : "Green", "/finance/invoices?status=PendingCertification"),
            new("CRITICAL_RISKS", "Critical risks", critical, null, critical > 0 ? "Red" : "Green", "/assurance/risks?rating=Critical"),
            new("OVERDUE_ACTIONS", "Overdue corrective actions", overdueActions, null, overdueActions > 0 ? "Red" : "Green", "/me/actions?overdue=true"),
            new("OPEN_AUDIT_FINDINGS", "Open audit findings", openAuditFindings, null, openAuditFindings > 0 ? "Amber" : "Green", "/assurance/audit-findings")
        };

        var health = Enum.GetValues<HealthStatus>()
            .Select(h => new HealthBucket(h.ToString(), active.Count(p => p.Project.Health == h))).ToList();

        var programmes = projects.Values.GroupBy(p => p.Project.ProgrammeId).Select(g =>
        {
            var ids = g.Select(p => p.Id).ToHashSet();
            return new ProgrammeRollupDto(g.Key, g.First().ProgrammeName, g.First().PortfolioName, g.Count(),
                g.Count(p => p.Project.Health == HealthStatus.Red), g.Count(p => p.Project.Health == HealthStatus.Amber),
                g.Count(p => p.Project.Health == HealthStatus.Green), budget.Where(b => ids.Contains(b.ProjectId)).Sum(b => b.RevisedAmount),
                actual.Where(a => ids.Contains(a.ProjectId)).Sum(a => a.Amount));
        }).OrderByDescending(p => p.Budget).ToList();

        var exceptions = await BuildExceptionsAsync(new ReportFilter(fy), ct);
        var top = exceptions.OrderBy(e => SeverityRank(e.Severity)).ThenByDescending(e => e.DaysOverdue ?? 0).Take(15).ToList();

        return new ExecutiveDashboardDto(fy, kpis, health, programmes, performance, top);
    }

    private static int SeverityRank(string s) => s switch { "Critical" => 0, "High" => 1, "Medium" => 2, _ => 3 };

    // ---------------- Exceptions (FR-REP-005) ----------------
    public async Task<IReadOnlyList<ExceptionItemDto>> ExceptionsAsync(ReportFilter filter, CancellationToken ct)
    {
        Validate(filter);
        return await BuildExceptionsAsync(filter, ct);
    }

    public async Task<ExportedFile> ExportExceptionsAsync(ReportFilter filter, string format, CancellationToken ct)
    {
        var items = await ExceptionsAsync(filter, ct);
        var table = new ReportTable("EXCEPTIONS", "Exception report", _clock.UtcNow,
            new[] { "Category", "Reference", "Description", "Severity", "Project", "Due date", "Days overdue" },
            items.Select(e => new object?[] { e.Category, e.Reference, e.Description, e.Severity, e.ProjectReference, e.DueDate, e.DaysOverdue }).ToList(),
            filter.Describe());
        var file = ReportExport.Export(table, format);
        await LogExportAsync(table, format, ct);
        return file;
    }

    private async Task<List<ExceptionItemDto>> BuildExceptionsAsync(ReportFilter filter, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var (effective, projects) = await _builder.ResolveAsync(filter, scope, ct);
        var today = _clock.Today;
        var items = new List<ExceptionItemDto>();
        string? Ref(Guid? id) => id is { } g && projects.TryGetValue(g, out var p) ? p.Reference : null;
        int? Overdue(DateOnly due) => due < today ? today.DayNumber - due.DayNumber : null;

        // At-risk projects.
        foreach (var p in projects.Values.Where(p => p.Project.Health == HealthStatus.Red && p.Project.Status is not (ProjectStatus.Closed or ProjectStatus.Cancelled)))
        {
            items.Add(new ExceptionItemDto("At-risk project", p.Reference, $"{p.Project.Name}: {p.Project.HealthExplanation}", "High", p.Id, p.Reference, null, null,
                $"/projects/{p.Id}"));
        }

        // Over-budget projects (forecast or actual+committed above revised budget by the configured percentage).
        var threshold = await _settings.GetDecimalAsync(SettingKeys.ExceptionOverBudgetPercent, ct);
        var lines = await _db.BudgetLines.AsNoTracking().InScope(effective, b => b.ProjectId).Select(b => new { b.ProjectId, b.RevisedAmount }).ToListAsync(ct);
        var spend = await _db.Expenditures.AsNoTracking().InScope(effective, e => e.ProjectId).Select(e => new { e.ProjectId, e.Amount }).ToListAsync(ct);
        var commits = await _db.Commitments.AsNoTracking().InScope(effective, c => c.ProjectId).Where(c => !c.IsReleased)
            .Select(c => new { c.ProjectId, c.Amount }).ToListAsync(ct);
        var forecasts = (await _db.CostForecasts.AsNoTracking().InScope(effective, f => f.ProjectId).ToListAsync(ct))
            .GroupBy(f => f.ProjectId).ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.RecordedAtUtc).First().EstimateAtCompletion);
        foreach (var p in projects.Values)
        {
            var budget = lines.Where(l => l.ProjectId == p.Id).Sum(l => l.RevisedAmount);
            if (budget <= 0) budget = p.Project.ApprovedBudget;
            if (budget <= 0) continue;
            var exposure = Math.Max(spend.Where(s => s.ProjectId == p.Id).Sum(s => s.Amount) + commits.Where(c => c.ProjectId == p.Id).Sum(c => c.Amount),
                forecasts.GetValueOrDefault(p.Id));
            var over = (exposure - budget) / budget * 100m;
            if (over > threshold)
            {
                items.Add(new ExceptionItemDto("Over budget", p.Reference, $"{p.Project.Name}: exposure {exposure:N2} vs budget {budget:N2} ({over:0.#}% over)",
                    over > 10 ? "High" : "Medium", p.Id, p.Reference, null, null, $"/projects/{p.Id}/finance"));
            }
        }

        // Overdue milestones.
        var milestones = await _db.WbsElements.AsNoTracking().InScope(effective, w => w.ProjectId)
            .Where(w => w.Type == WbsType.Milestone && w.Status != WorkStatus.Completed && w.Status != WorkStatus.Cancelled && w.PlannedEnd != null && w.PlannedEnd < today)
            .ToListAsync(ct);
        items.AddRange(milestones.Select(m => new ExceptionItemDto("Overdue milestone", m.Code, m.Name, m.IsCritical ? "High" : "Medium", m.ProjectId, Ref(m.ProjectId),
            m.PlannedEnd, Overdue(m.PlannedEnd!.Value), $"/projects/{m.ProjectId}/schedule")));

        // Overdue issues, corrective actions, risk treatments and audit findings.
        var issues = await _db.Issues.AsNoTracking().InScope(effective, i => i.ProjectId)
            .Where(i => i.DueDate != null && i.DueDate < today && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed).ToListAsync(ct);
        items.AddRange(issues.Select(i => new ExceptionItemDto("Overdue issue", i.Number, i.Title, i.Severity.ToString(), i.ProjectId, Ref(i.ProjectId), i.DueDate,
            Overdue(i.DueDate!.Value), $"/projects/{i.ProjectId}/issues")));

        var actions = await _db.CorrectiveActions.AsNoTracking().InScopeNullable(effective, a => a.ProjectId)
            .Where(a => (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress) && a.DueDate < today).ToListAsync(ct);
        items.AddRange(actions.Select(a => new ExceptionItemDto("Overdue corrective action", a.Number, a.Description, a.EscalationLevel > 0 ? "High" : "Medium",
            a.ProjectId, Ref(a.ProjectId), a.DueDate, Overdue(a.DueDate), "/me/actions")));

        var treatments = await _db.RiskTreatments.AsNoTracking().InScopeNullable(effective, t => t.ProjectId)
            .Where(t => (t.Status == ActionStatus.Open || t.Status == ActionStatus.InProgress) && t.DueDate < today).ToListAsync(ct);
        items.AddRange(treatments.Select(t => new ExceptionItemDto("Overdue risk mitigation", t.Id.ToString("N")[..8].ToUpperInvariant(), t.Description, "High",
            t.ProjectId, Ref(t.ProjectId), t.DueDate, Overdue(t.DueDate), $"/assurance/risks/{t.RiskId}")));

        if (effective.All)
        {
            var findings = await _db.AuditFindings.AsNoTracking().Where(f => f.Status != FindingStatus.Closed && f.DueDate < today).ToListAsync(ct);
            items.AddRange(findings.Select(f => new ExceptionItemDto("Overdue audit finding", f.Number, f.Title, f.Rating.ToString(), f.ProjectId, Ref(f.ProjectId),
                f.DueDate, Overdue(f.DueDate), "/assurance/audit-findings")));

            var attestations = (await _db.ComplianceAttestations.AsNoTracking().ToListAsync(ct))
                .GroupBy(a => a.ObligationId).Select(g => g.OrderByDescending(a => a.AttestedAtUtc).First())
                .Where(a => a.Result is AttestationResult.NonCompliant or AttestationResult.PartiallyCompliant).ToList();
            var obligations = await _db.ComplianceObligations.AsNoTracking().ToDictionaryAsync(o => o.Id, ct);
            items.AddRange(attestations.Select(a => new ExceptionItemDto("Non-compliance", obligations.GetValueOrDefault(a.ObligationId)?.Code ?? "?",
                $"{obligations.GetValueOrDefault(a.ObligationId)?.Title}: {a.Result} ({a.Period})",
                a.Result == AttestationResult.NonCompliant ? "High" : "Medium", null, null, null, null, "/assurance/compliance")));
        }

        // Aged procurements.
        var ageing = await _settings.GetIntAsync(SettingKeys.ExceptionProcurementAgeingDays, ct);
        var cutoff = _clock.UtcNow.AddDays(-ageing);
        var procurements = await _db.Procurements.AsNoTracking().InScope(effective, p => p.ProjectId)
            .Where(p => p.Status != ProcurementStatus.Awarded && p.Status != ProcurementStatus.Cancelled && p.CreatedAtUtc < cutoff).ToListAsync(ct);
        items.AddRange(procurements.Select(p => new ExceptionItemDto("Aged procurement", p.Number,
            $"{p.Title} in {p.Status} for {(int)(_clock.UtcNow - p.CreatedAtUtc).TotalDays} days", "Medium", p.ProjectId, Ref(p.ProjectId), null,
            (int)(_clock.UtcNow - p.CreatedAtUtc).TotalDays - ageing, $"/procurement/{p.Id}")));

        // Contracts active beyond end date, and expiring within 30 days.
        var contracts = await _db.Contracts.AsNoTracking().InScope(effective, c => c.ProjectId)
            .Where(c => c.Status == ContractStatus.Active).ToListAsync(ct);
        foreach (var c in contracts)
        {
            var days = c.CurrentEndDate.DayNumber - today.DayNumber;
            if (days < 0)
                items.Add(new ExceptionItemDto("Contract past end date", c.ContractNumber, $"{c.Title} ended {c.CurrentEndDate:yyyy-MM-dd} but is still active",
                    "High", c.ProjectId, Ref(c.ProjectId), c.CurrentEndDate, -days, $"/contracts/{c.Id}"));
            else if (days <= 30)
                items.Add(new ExceptionItemDto("Contract expiring", c.ContractNumber, $"{c.Title} ends in {days} day(s)", "Medium", c.ProjectId, Ref(c.ProjectId),
                    c.CurrentEndDate, null, $"/contracts/{c.Id}"));
        }

        // Invoices unpaid after 30 days (PFMA / Treasury 30-day payment rule).
        var invoiceCutoff = today.AddDays(-30);
        var invoices = await _db.Invoices.AsNoTracking().InScope(effective, i => i.ProjectId)
            .Where(i => i.ReceivedDate < invoiceCutoff && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Rejected && i.Status != InvoiceStatus.Cancelled)
            .ToListAsync(ct);
        items.AddRange(invoices.Select(i => new ExceptionItemDto("Invoice unpaid > 30 days", i.Number, $"{i.SupplierInvoiceNumber} {i.Amount:N2} ({i.Status})", "High",
            i.ProjectId, Ref(i.ProjectId), i.ReceivedDate.AddDays(30), Overdue(i.ReceivedDate.AddDays(30)), $"/finance/invoices/{i.Id}")));

        return items.OrderBy(i => SeverityRank(i.Severity)).ThenByDescending(i => i.DaysOverdue ?? 0).ToList();
    }

    // ---------------- Board packs (FR-REP-003) ----------------
    private static BoardPackDto ToDto(BoardReportPack p) => new(p.Id, p.Number, p.Period, p.PackVersion, p.Title, p.Status.ToString(), p.GeneratedAtUtc,
        p.GeneratedBy, p.ApprovedAtUtc, p.ApprovedBy);

    public async Task<IReadOnlyList<BoardPackDto>> ListBoardPacksAsync(string? period, CancellationToken ct)
    {
        var packs = await _db.BoardReportPacks.AsNoTracking().Where(p => period == null || p.Period == period)
            .OrderByDescending(p => p.GeneratedAtUtc).ToListAsync(ct);
        return packs.Select(ToDto).ToList();
    }

    public async Task<BoardPackDetailDto> GetBoardPackAsync(Guid id, CancellationToken ct)
    {
        var pack = await _db.BoardReportPacks.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Board pack", id);
        using var doc = JsonDocument.Parse(pack.DatasetJson);
        return new BoardPackDetailDto(ToDto(pack), doc.RootElement.Clone());
    }

    private static readonly JsonSerializerOptions BoardPackDatasetOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Renders the frozen pack dataset as a board/client-ready PDF (charts, KPI cards and detail tables), reusing the
    /// same builder as the live Executive Summary export so both documents look and read consistently (FR-REP-003).
    /// </summary>
    public async Task<ExportedFile> ExportBoardPackAsync(Guid id, CancellationToken ct)
    {
        var pack = await _db.BoardReportPacks.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Board pack", id);
        var dataset = JsonSerializer.Deserialize<BoardPackDatasetDto>(pack.DatasetJson, BoardPackDatasetOptions)
            ?? throw new NotFoundException("Board pack dataset", id);
        var dashboard = new ExecutiveDashboardDto(dataset.FinancialYear, dataset.Kpis, dataset.Health, dataset.Programmes, dataset.AppPerformance,
            dataset.Exceptions);

        var title = $"TETA - Board Pack {pack.Number} (v{pack.PackVersion})";
        var subtitle = $"{pack.Title}  |  Period {pack.Period}  |  Status {pack.Status}  |  Generated {pack.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC by {pack.GeneratedBy}" +
            (pack.ApprovedAtUtc is { } approvedAt ? $"  |  Approved {approvedAt:yyyy-MM-dd HH:mm} UTC by {pack.ApprovedBy}" : string.Empty);

        var extraTables = new List<(string Heading, string Subheading, string[] Columns, IReadOnlyList<object?[]> Rows)>
        {
            ("Portfolio Health Detail", "Frozen at the time this pack was generated", dataset.PortfolioHealth.Columns.ToArray(),
                dataset.PortfolioHealth.Rows.Select(r => r.Cast<object?>().ToArray()).ToList()),
            ("Evidence Verification Detail", "Frozen at the time this pack was generated", dataset.EvidenceVerification.Columns.ToArray(),
                dataset.EvidenceVerification.Rows.Select(r => r.Cast<object?>().ToArray()).ToList())
        };

        var bytes = ExecutiveSummaryReportService.BuildPdf(dashboard, pack.GeneratedAtUtc, title, subtitle, extraTables);
        _audit.Write("Reporting", "BoardPack", pack.Number, "Export", new { format = "pdf", pack.Number, pack.PackVersion });
        await _db.SaveChangesAsync(ct);
        return new ExportedFile(bytes, "application/pdf", $"{pack.Number}-v{pack.PackVersion}.pdf");
    }

    /// <summary>Freezes the executive dataset for a reporting period as a new pack version; earlier versions are retained.</summary>
    public async Task<BoardPackDto> GenerateBoardPackAsync(GenerateBoardPackRequest request, CancellationToken ct)
    {
        new Validator().Required("period", request.Period, 40).Required("title", request.Title, 200).ThrowIfInvalid();
        var dashboard = await ExecutiveDashboardAsync(request.FinancialYear, ct);
        var exceptions = await BuildExceptionsAsync(new ReportFilter(dashboard.FinancialYear), ct);
        var scope = await _scope.GetAsync(ct);
        var health = await _builder.BuildAsync("RPT-002", new ReportFilter(), scope, ct);
        var evidence = await _builder.BuildAsync("RPT-013", new ReportFilter(dashboard.FinancialYear), scope, ct);

        var dataset = new
        {
            request.Period,
            dashboard.FinancialYear,
            GeneratedAtUtc = _clock.UtcNow,
            GeneratedBy = _user.DisplayName ?? _user.Username,
            dashboard.Kpis,
            dashboard.Health,
            dashboard.Programmes,
            dashboard.AppPerformance,
            Exceptions = exceptions,
            PortfolioHealth = new { health.Columns, Rows = health.Rows.Select(r => r.Select(ReportExport.FormatCell).ToArray()) },
            EvidenceVerification = new { evidence.Columns, Rows = evidence.Rows.Select(r => r.Select(ReportExport.FormatCell).ToArray()) }
        };

        var latest = await _db.BoardReportPacks.Where(p => p.Period == request.Period).Select(p => (int?)p.PackVersion).MaxAsync(ct) ?? 0;
        var pack = new BoardReportPack
        {
            Number = await _numbers.NextAsync(NumberPrefixes.BoardPack, ct),
            Period = request.Period.Trim(),
            PackVersion = latest + 1,
            Title = request.Title.Trim(),
            Status = ApprovalState.Draft,
            DatasetJson = JsonSerializer.Serialize(dataset),
            GeneratedAtUtc = _clock.UtcNow,
            GeneratedBy = _user.DisplayName ?? _user.Username
        };
        _db.BoardReportPacks.Add(pack);
        await _db.SaveChangesAsync(ct);
        return ToDto(pack);
    }

    public async Task<BoardPackDto> ApproveBoardPackAsync(Guid id, CancellationToken ct)
    {
        var pack = await _db.BoardReportPacks.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Board pack", id);
        if (pack.Status == ApprovalState.Approved) throw new DomainException("This pack version is already approved.", "FR-REP-003");
        if (pack.CreatedByUserId == _user.UserId)
            throw new DomainException("The person who generated a Board pack cannot approve it (segregation of duties).", "BR-005");
        pack.Status = ApprovalState.Approved;
        pack.ApprovedAtUtc = _clock.UtcNow;
        pack.ApprovedBy = _user.DisplayName ?? _user.Username;
        await _db.SaveChangesAsync(ct);
        return ToDto(pack);
    }

    // ---------------- Scheduled reports (FR-REP-006) ----------------
    private sealed record ScheduleFilter(string? FinancialYear, Guid? ProgrammeId, Guid? ProjectId);

    private static ReportScheduleDto ToDto(ReportSchedule s) => new(s.Id, s.ReportCode, ReportCatalogue.All.FirstOrDefault(r => r.Code == s.ReportCode)?.Name ?? s.ReportCode,
        s.Format, s.Frequency, s.Recipients, s.IsEnabled, s.OwnerUserId, s.LastRunAtUtc, s.NextRunAtUtc, s.LastRunStatus, s.Version);

    public async Task<IReadOnlyList<ReportScheduleDto>> ListSchedulesAsync(CancellationToken ct)
    {
        var userId = _user.UserId;
        var all = _user.HasPermission(Permissions.AdminConfig);
        var schedules = await _db.ReportSchedules.AsNoTracking().Where(s => all || s.OwnerUserId == userId).OrderBy(s => s.ReportCode).ToListAsync(ct);
        return schedules.Select(ToDto).ToList();
    }

    public async Task<ReportScheduleDto> SaveScheduleAsync(Guid? id, SaveScheduleRequest request, CancellationToken ct)
    {
        var definition = ReportCatalogue.Get(request.ReportCode);
        if (!_user.HasPermission(definition.Permission) || !_user.HasPermission(Permissions.ReportsExport))
            throw new ForbiddenException("You are not authorised to schedule this report.");

        var recipients = request.Recipients.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        new Validator()
            .OneOf("format", request.Format, ReportExport.Formats)
            .OneOf("frequency", request.Frequency, Frequencies)
            .Must(recipients.Length is > 0 and <= 20 && recipients.All(r => r.Contains('@') && r.Length <= 200), "recipients",
                "Provide 1-20 valid e-mail addresses separated by commas.")
            .Must(request.FinancialYear is null || Fy.IsValid(request.FinancialYear), "financialYear", "Use the format 2026/27.")
            .ThrowIfInvalid();

        ReportSchedule schedule;
        if (id is { } existingId)
        {
            schedule = await _db.ReportSchedules.SingleOrDefaultAsync(s => s.Id == existingId, ct) ?? throw new NotFoundException("Report schedule", existingId);
            if (schedule.OwnerUserId != _user.UserId && !_user.HasPermission(Permissions.AdminConfig))
                throw new ForbiddenException("Only the schedule owner can change it.");
        }
        else
        {
            schedule = new ReportSchedule { OwnerUserId = _user.UserId ?? throw new ForbiddenException("Sign in required.") };
            _db.ReportSchedules.Add(schedule);
        }

        var frequencyChanged = schedule.Frequency != request.Frequency || schedule.NextRunAtUtc is null;
        schedule.ReportCode = definition.Code;
        schedule.Format = request.Format.ToLowerInvariant();
        schedule.Frequency = request.Frequency;
        schedule.Recipients = string.Join(",", recipients);
        schedule.IsEnabled = request.IsEnabled;
        schedule.LastRunStatus = SerializeFilter(new ScheduleFilter(request.FinancialYear, request.ProgrammeId, request.ProjectId), schedule.LastRunStatus);
        if (frequencyChanged || !request.IsEnabled) schedule.NextRunAtUtc = request.IsEnabled ? NextRun(_clock.UtcNow, request.Frequency) : null;
        await _db.SaveChangesAsync(ct);
        return ToDto(schedule);
    }

    // The schedule's filter is kept alongside the last-run status as "filter-json|status" to avoid a schema change.
    private static string SerializeFilter(ScheduleFilter filter, string? previousStatus)
    {
        var status = previousStatus?.Contains('|') == true ? previousStatus[(previousStatus.IndexOf('|') + 1)..] : previousStatus;
        return JsonSerializer.Serialize(filter) + "|" + (status ?? "Not run");
    }

    private static ScheduleFilter ReadFilter(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith('{') || !stored.Contains('|')) return new ScheduleFilter(null, null, null);
        try
        {
            return JsonSerializer.Deserialize<ScheduleFilter>(stored[..stored.IndexOf('|')]) ?? new ScheduleFilter(null, null, null);
        }
        catch (JsonException)
        {
            return new ScheduleFilter(null, null, null);
        }
    }

    internal static DateTime NextRun(DateTime fromUtc, string frequency)
    {
        var baseTime = fromUtc.Date.AddHours(5); // 05:00 UTC = 07:00 SAST
        return frequency switch
        {
            "Daily" => baseTime <= fromUtc ? baseTime.AddDays(1) : baseTime,
            "Weekly" => NextMonday(baseTime, fromUtc),
            "Monthly" => new DateTime(fromUtc.Year, fromUtc.Month, 1, 5, 0, 0, DateTimeKind.Utc).AddMonths(1),
            _ => new DateTime(fromUtc.Year, ((fromUtc.Month - 1) / 3) * 3 + 1, 1, 5, 0, 0, DateTimeKind.Utc).AddMonths(3)
        };
    }

    private static DateTime NextMonday(DateTime baseTime, DateTime fromUtc)
    {
        var days = ((int)DayOfWeek.Monday - (int)baseTime.DayOfWeek + 7) % 7;
        var candidate = baseTime.AddDays(days);
        return candidate <= fromUtc ? candidate.AddDays(7) : candidate;
    }

    /// <summary>Runs due schedules on the owner's behalf: owner's permissions and row-level scope apply; e-mail goes through the outbox.</summary>
    public async Task<int> RunDueSchedulesAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var due = await _db.ReportSchedules.Where(s => s.IsEnabled && s.NextRunAtUtc != null && s.NextRunAtUtc <= now).ToListAsync(ct);
        var count = 0;
        foreach (var schedule in due)
        {
            string status;
            try
            {
                var definition = ReportCatalogue.Get(schedule.ReportCode);
                var today = _clock.Today;
                var ownerRoles = await _db.UserRoleAssignments.AsNoTracking()
                    .Where(a => a.UserId == schedule.OwnerUserId && a.Status == AssignmentStatus.Active && a.EffectiveFrom <= today
                                && (a.EffectiveTo == null || a.EffectiveTo >= today))
                    .Select(a => a.RoleCode).Distinct().ToListAsync(ct);
                var ownerPermissions = await _db.RolePermissions.AsNoTracking().Where(p => ownerRoles.Contains(p.RoleCode))
                    .Select(p => p.Permission).Distinct().ToListAsync(ct);
                var ownerActive = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == schedule.OwnerUserId && u.IsActive, ct);
                if (!ownerActive || !ownerPermissions.Contains(definition.Permission) || !ownerPermissions.Contains(Permissions.ReportsExport))
                {
                    schedule.IsEnabled = false;
                    status = "Disabled: owner no longer authorised";
                }
                else
                {
                    var scope = await AccessScopeService.ForUserAsync(_db, schedule.OwnerUserId, today, ct);
                    var stored = ReadFilter(schedule.LastRunStatus);
                    var table = await _builder.BuildAsync(definition.Code, new ReportFilter(stored.FinancialYear, ProgrammeId: stored.ProgrammeId,
                        ProjectId: stored.ProjectId), scope, ct);
                    var file = ReportExport.Export(table, schedule.Format);
                    var preview = Encoding.UTF8.GetString(ReportExport.Csv(table with { Rows = table.Rows.Take(200).ToList() })).TrimStart('﻿');
                    var body = $"<p>Scheduled report <b>{System.Net.WebUtility.HtmlEncode(table.Title)}</b> ({table.RowCount} rows, generated {table.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC).</p>" +
                               $"<p>Attachment: {System.Net.WebUtility.HtmlEncode(file.FileName)}</p><pre>{System.Net.WebUtility.HtmlEncode(preview)}</pre>";
                    _db.OutboxMessages.Add(new OutboxMessage
                    {
                        OccurredAtUtc = now,
                        Type = "Email",
                        PayloadJson = JsonSerializer.Serialize(new EmailOutboxPayload(
                            schedule.Recipients.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                            $"Scheduled report: {table.Code} {table.Title}", body, file.FileName, file.ContentType, Convert.ToBase64String(file.Content)))
                    });
                    _audit.Write("Reporting", "ReportSchedule", schedule.Id.ToString(), "ScheduledExport",
                        new { table.Code, schedule.Format, table.RowCount, schedule.Recipients });
                    status = $"Sent {table.RowCount} row(s)";
                    count++;
                }
            }
            catch (Exception ex) when (ex is PlatformException or JsonException or InvalidOperationException)
            {
                status = "Failed: " + ex.Message;
            }

            schedule.LastRunAtUtc = now;
            schedule.LastRunStatus = SerializeFilter(ReadFilter(schedule.LastRunStatus), status.Length > 150 ? status[..150] : status);
            schedule.NextRunAtUtc = schedule.IsEnabled ? NextRun(now, schedule.Frequency) : null;
            await _db.SaveChangesAsync(ct);
        }
        return count;
    }

    // ---------------- Ad-hoc analytics (FR-REP-007) ----------------
    public async Task<ReportTable> AnalyticsAsync(ReportFilter filter, CancellationToken ct)
    {
        Validate(filter);
        var scope = await _scope.GetAsync(ct);
        var (effective, projects) = await _builder.ResolveAsync(filter, scope, ct);
        var fy = filter.FinancialYear;

        var budget = await _db.BudgetLines.AsNoTracking().InScope(effective, b => b.ProjectId).Where(b => fy == null || b.FinancialYear == fy)
            .Select(b => new { b.ProjectId, b.RevisedAmount }).ToListAsync(ct);
        var actual = await _db.Expenditures.AsNoTracking().InScope(effective, e => e.ProjectId).Where(e => fy == null || e.FinancialYear == fy)
            .Select(e => new { e.ProjectId, e.Amount }).ToListAsync(ct);
        var committed = await _db.Commitments.AsNoTracking().InScope(effective, c => c.ProjectId).Where(c => !c.IsReleased && (fy == null || c.FinancialYear == fy))
            .Select(c => new { c.ProjectId, c.Amount }).ToListAsync(ct);
        var risks = await _db.Risks.AsNoTracking().InScopeNullable(effective, r => r.ProjectId).Where(r => r.Status != RiskStatus.Closed)
            .Select(r => new { r.ProjectId, r.ResidualRating }).ToListAsync(ct);
        var issues = await _db.Issues.AsNoTracking().InScope(effective, i => i.ProjectId).Where(i => i.Status != IssueStatus.Closed && i.Status != IssueStatus.Resolved)
            .Select(i => i.ProjectId).ToListAsync(ct);
        var contracts = await _db.Contracts.AsNoTracking().InScope(effective, c => c.ProjectId)
            .Select(c => new { c.ProjectId, c.OriginalValue, c.ApprovedVariations, c.Status }).ToListAsync(ct);
        var procurements = await _db.Procurements.AsNoTracking().InScope(effective, p => p.ProjectId).Select(p => new { p.ProjectId, p.Status }).ToListAsync(ct);
        var beneficiaries = await _db.Beneficiaries.AsNoTracking().InScope(effective, b => b.ProjectId).Select(b => b.ProjectId).ToListAsync(ct);
        var results = await _db.PerformanceResults.AsNoTracking().InScope(effective, r => r.ProjectId)
            .Where(r => r.Status == ResultStatus.Verified && (fy == null || r.FinancialYear == fy)).Select(r => r.ProjectId).ToListAsync(ct);

        var columns = new[]
        {
            "Project", "Name", "Portfolio", "Programme", "Status", "Stage", "Health", "Project type", "Org unit", "Province", "District",
            "Municipality", "Planned start", "Planned end", "Approved budget", "Revised budget", "Committed", "Actual", "Open risks", "Critical/high risks",
            "Open issues", "Contracts", "Active contract value", "Procurements", "Open procurements", "Beneficiaries", "Verified APP results"
        };
        var rows = projects.Values
            .Where(p => string.IsNullOrWhiteSpace(filter.Status) || string.Equals(p.Project.Status.ToString(), filter.Status, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(p.Project.Health.ToString(), filter.Status, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Reference)
            .Select(p => new object?[]
            {
                p.Reference, p.Project.Name, p.PortfolioName, p.ProgrammeName, p.Project.Status.ToString(), p.Project.Stage.ToString(), p.Project.Health.ToString(),
                p.Project.ProjectType, p.Project.OrgUnit, p.Project.Province, p.Project.District, p.Project.Municipality, p.Project.PlannedStart,
                p.Project.PlannedEnd, p.Project.ApprovedBudget, budget.Where(b => b.ProjectId == p.Id).Sum(b => b.RevisedAmount),
                committed.Where(c => c.ProjectId == p.Id).Sum(c => c.Amount), actual.Where(a => a.ProjectId == p.Id).Sum(a => a.Amount),
                risks.Count(r => r.ProjectId == p.Id), risks.Count(r => r.ProjectId == p.Id && r.ResidualRating is "Critical" or "High"),
                issues.Count(i => i == p.Id), contracts.Count(c => c.ProjectId == p.Id),
                contracts.Where(c => c.ProjectId == p.Id && c.Status == ContractStatus.Active).Sum(c => c.OriginalValue + c.ApprovedVariations),
                procurements.Count(x => x.ProjectId == p.Id),
                procurements.Count(x => x.ProjectId == p.Id && x.Status != ProcurementStatus.Awarded && x.Status != ProcurementStatus.Cancelled),
                beneficiaries.Count(b => b == p.Id), results.Count(r => r == p.Id)
            }).ToList();
        return new ReportTable("ANALYTICS", "Portfolio analytical dataset", _clock.UtcNow, columns, rows, filter.Describe());
    }

    // ---------------- Geographic view (FR-REP-009) ----------------
    public async Task<GeoViewDto> GeographicAsync(string level, ReportFilter filter, CancellationToken ct)
    {
        level = level switch { "district" or "District" => "District", "municipality" or "Municipality" => "Municipality", _ => "Province" };
        var scope = await _scope.GetAsync(ct);
        var (effective, projects) = await _builder.ResolveAsync(filter, scope, ct);
        var minimum = Math.Max(1, await _settings.GetIntAsync(SettingKeys.GeoMinimumGroupSize, ct));
        var beneficiaries = await _db.Beneficiaries.AsNoTracking().InScope(effective, b => b.ProjectId)
            .Select(b => new { b.ProjectId, b.Province, b.District }).ToListAsync(ct);

        (string Province, string? District, string? Municipality) KeyOf(string? province, string? district, string? municipality) => level switch
        {
            "Municipality" => (province ?? "Unknown", district, municipality),
            "District" => (province ?? "Unknown", district, null),
            _ => (province ?? "Unknown", null, null)
        };

        var located = projects.Values.Where(p => !string.IsNullOrWhiteSpace(p.Project.Province)).ToList();
        var areaKeys = located.Select(p => KeyOf(p.Project.Province, p.Project.District, p.Project.Municipality))
            .Concat(level == "Municipality" ? Enumerable.Empty<(string Province, string? District, string? Municipality)>()
                : beneficiaries.Where(b => !string.IsNullOrWhiteSpace(b.Province)).Select(b => KeyOf(b.Province, b.District, null)))
            .Distinct().ToList();

        var areas = areaKeys.Select(key =>
        {
            var inArea = located.Where(p => KeyOf(p.Project.Province, p.Project.District, p.Project.Municipality) == key).ToList();
            // Beneficiaries are only aggregated to province/district (no municipality attribute is captured).
            int? count = level == "Municipality" ? null : beneficiaries.Count(b => KeyOf(b.Province, b.District, null) == key);
            var suppressed = count is > 0 && count < minimum;
            return new GeoAreaDto(key.Province, key.District, key.Municipality, inArea.Count, inArea.Sum(p => p.Project.ApprovedBudget),
                suppressed ? null : count, suppressed);
        }).OrderBy(a => a.Province).ThenBy(a => a.District).ThenBy(a => a.Municipality).ToList();

        return new GeoViewDto(level, minimum, areas, projects.Count - located.Count);
    }

    // ---------------- Data quality (FR-REP-010) ----------------
    private static DataQualityIssueDto ToDto(DataQualityIssue i) => new(i.Id, i.RuleCode, i.Description, i.EntityType, i.EntityId, i.EntityReference,
        i.Severity.ToString(), i.Status.ToString(), i.OwnerUserId, i.OwnerName, i.Resolution, i.DetectedAtUtc, i.ResolvedAtUtc, i.Version);

    public async Task<DataQualityDashboardDto> DataQualityAsync(string? status, string? ruleCode, CancellationToken ct)
    {
        var all = await _db.DataQualityIssues.AsNoTracking().ToListAsync(ct);
        var canManage = _user.HasPermission(Permissions.DataQualityManage);
        var visible = canManage ? all : all.Where(i => i.OwnerUserId == _user.UserId).ToList();
        var filtered = visible
            .Where(i => status is null || string.Equals(i.Status.ToString(), status, StringComparison.OrdinalIgnoreCase))
            .Where(i => ruleCode is null || i.RuleCode == ruleCode)
            .OrderBy(i => i.Status).ThenByDescending(i => i.Severity).ThenBy(i => i.DetectedAtUtc).Take(1000).ToList();
        var since = _clock.UtcNow.AddDays(-30);
        return new DataQualityDashboardDto(
            visible.Count(i => i.Status == DataQualityStatus.Open),
            visible.Count(i => i.Status != DataQualityStatus.Open && i.ResolvedAtUtc >= since),
            visible.Where(i => i.Status == DataQualityStatus.Open).GroupBy(i => i.RuleCode).Select(g => new RuleCount(g.Key, g.Count()))
                .OrderByDescending(r => r.Open).ToList(),
            await _db.ErpInterfaceMessages.CountAsync(m => m.Status == InterfaceStatus.Error, ct),
            await _db.ErpReconciliations.CountAsync(r => !r.Balanced, ct),
            filtered.Select(ToDto).ToList());
    }

    public async Task<DataQualityIssueDto> ResolveIssueAsync(Guid id, ResolveIssueRequest request, CancellationToken ct)
    {
        new Validator().Required("resolution", request.Resolution, 1000)
            .Must(request.Status != DataQualityStatus.Open, "status", "Choose Resolved, Closed or Ignored.")
            .ThrowIfInvalid();
        var issue = await _db.DataQualityIssues.SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Data quality issue", id);
        if (issue.OwnerUserId != _user.UserId && !_user.HasPermission(Permissions.DataQualityManage))
            throw new ForbiddenException("Only the issue owner or a data-quality manager can resolve this issue.");
        issue.Status = request.Status;
        issue.Resolution = request.Resolution.Trim();
        issue.ResolvedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(issue);
    }

    private sealed record Detected(string RuleCode, string Description, string EntityType, Guid EntityId, string? Reference, Severity Severity,
        Guid? OwnerUserId, string? OwnerName);

    /// <summary>
    /// Runs the data-quality rules: raises new issues, keeps existing open ones, and auto-resolves open
    /// issues whose condition has cleared. Issues that an owner closed/ignored are not re-raised.
    /// </summary>
    public async Task<int> ScanDataQualityAsync(CancellationToken ct)
    {
        var today = _clock.Today;
        var detected = new List<Detected>();

        var projects = await _db.Projects.AsNoTracking().Where(p => p.Status != ProjectStatus.Closed && p.Status != ProjectStatus.Cancelled
                                                                   && p.Status != ProjectStatus.Rejected).ToListAsync(ct);
        var budgetedProjects = (await _db.BudgetLines.AsNoTracking().Select(b => b.ProjectId).Distinct().ToListAsync(ct)).ToHashSet();
        foreach (var p in projects)
        {
            var owner = p.ManagerUserId ?? p.SponsorUserId;
            var ownerName = p.ManagerName ?? p.SponsorName;
            if (p.ManagerUserId is null && p.Status is ProjectStatus.Approved or ProjectStatus.InExecution)
                detected.Add(new("DQ-PRJ-001", $"Project {p.Reference} has no project manager assigned.", "Project", p.Id, p.Reference, Severity.High, p.SponsorUserId, p.SponsorName));
            if (p.Status is ProjectStatus.Approved or ProjectStatus.InExecution && (p.PlannedStart is null || p.PlannedEnd is null))
                detected.Add(new("DQ-PRJ-002", $"Project {p.Reference} has no planned start/end date.", "Project", p.Id, p.Reference, Severity.Medium, owner, ownerName));
            if (p.Status is ProjectStatus.Approved or ProjectStatus.InExecution && !budgetedProjects.Contains(p.Id))
                detected.Add(new("DQ-PRJ-003", $"Project {p.Reference} has no budget lines.", "Project", p.Id, p.Reference, Severity.High, owner, ownerName));
            if (string.IsNullOrWhiteSpace(p.Province) && p.Status is not ProjectStatus.Concept)
                detected.Add(new("DQ-PRJ-004", $"Project {p.Reference} has no province/location (geographic reporting).", "Project", p.Id, p.Reference, Severity.Low, owner, ownerName));
            if (p.Status == ProjectStatus.InExecution && p.PlannedEnd is { } end && end < today)
                detected.Add(new("DQ-PRJ-005", $"Project {p.Reference} is in execution past its planned end date ({end:yyyy-MM-dd}).", "Project", p.Id, p.Reference, Severity.Medium, owner, ownerName));
        }

        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.Status == SupplierStatus.Active).ToListAsync(ct);
        foreach (var s in suppliers)
        {
            if (string.IsNullOrWhiteSpace(s.CsdNumber) || !s.CsdVerified)
                detected.Add(new("DQ-SUP-001", $"Active supplier {s.LegalName} has no verified CSD registration.", "Supplier", s.Id, s.SupplierNumber, Severity.High, null, "SCM"));
            if (s.TaxClearanceExpiry is { } expiry && expiry < today)
                detected.Add(new("DQ-SUP-002", $"Tax clearance of supplier {s.LegalName} expired on {expiry:yyyy-MM-dd}.", "Supplier", s.Id, s.SupplierNumber, Severity.Medium, null, "SCM"));
        }
        foreach (var group in suppliers.Where(s => !string.IsNullOrWhiteSpace(s.RegistrationNumber)).GroupBy(s => s.RegistrationNumber!.Trim().ToUpperInvariant()).Where(g => g.Count() > 1))
        {
            foreach (var s in group.Skip(1))
                detected.Add(new("DQ-SUP-003", $"Supplier {s.LegalName} shares registration number {group.Key} with {group.First().LegalName}.", "Supplier", s.Id,
                    s.SupplierNumber, Severity.High, null, "SCM"));
        }

        var contracts = await _db.Contracts.AsNoTracking().Where(c => c.Status == ContractStatus.Active).ToListAsync(ct);
        foreach (var c in contracts)
        {
            if (c.CurrentEndDate < today)
                detected.Add(new("DQ-CON-001", $"Contract {c.ContractNumber} is active but ended on {c.CurrentEndDate:yyyy-MM-dd}.", "Contract", c.Id, c.ContractNumber, Severity.High,
                    c.ContractManagerUserId, c.ContractManagerName));
            if (c.ContractManagerUserId is null)
                detected.Add(new("DQ-CON-002", $"Contract {c.ContractNumber} has no contract manager.", "Contract", c.Id, c.ContractNumber, Severity.Medium, null, "Contract management"));
        }

        var duplicates = await _db.Invoices.AsNoTracking().Where(i => i.PotentialDuplicate && i.Status != InvoiceStatus.Rejected && i.Status != InvoiceStatus.Cancelled)
            .ToListAsync(ct);
        detected.AddRange(duplicates.Select(i => new Detected("DQ-FIN-001", $"Invoice {i.Number} ({i.SupplierInvoiceNumber}) is a potential duplicate.", "Invoice", i.Id, i.Number,
            Severity.High, null, "Finance")));
        var interfaceErrors = await _db.ErpInterfaceMessages.AsNoTracking().Where(m => m.Status == InterfaceStatus.Error).ToListAsync(ct);
        detected.AddRange(interfaceErrors.Select(m => new Detected("DQ-FIN-002", $"ERP {m.Direction} {m.MessageType} {m.ExternalReference} failed: {m.Error}", "ErpInterfaceMessage",
            m.Id, m.ExternalReference, Severity.High, null, "Finance / ICT")));
        var unbalanced = await _db.ErpReconciliations.AsNoTracking().Where(r => !r.Balanced).ToListAsync(ct);
        detected.AddRange(unbalanced.Select(r => new Detected("DQ-FIN-003", $"ERP {r.MessageType} batch {r.BatchId} did not reconcile.", "ErpReconciliation", r.Id,
            r.BatchId.ToString(), Severity.High, null, "Finance")));

        var beneficiaries = await _db.Beneficiaries.AsNoTracking().Where(b => b.PotentialDuplicate).Select(b => new { b.Id, b.Number, b.ProjectId }).ToListAsync(ct);
        detected.AddRange(beneficiaries.Select(b => new Detected("DQ-BEN-001", $"Beneficiary {b.Number} is a potential duplicate.", "Beneficiary", b.Id, b.Number,
            Severity.Medium, null, "M&E")));

        var submitted = await _db.PerformanceResults.AsNoTracking().Where(r => r.Status == ResultStatus.Submitted).Select(r => new { r.Id, r.ProjectId, r.FinancialYear, r.Quarter })
            .ToListAsync(ct);
        var submittedIds = submitted.Select(r => r.Id).ToList();
        var withEvidence = (await _db.Evidence.AsNoTracking().Where(e => e.ParentType == ParentTypes.PerformanceResult && submittedIds.Contains(e.ParentId))
            .Select(e => e.ParentId).Distinct().ToListAsync(ct)).ToHashSet();
        detected.AddRange(submitted.Where(r => !withEvidence.Contains(r.Id)).Select(r => new Detected("DQ-APP-001",
            $"Performance result for {r.FinancialYear} Q{r.Quarter} was submitted without evidence.", "PerformanceResult", r.Id, null, Severity.High, null, "Strategy")));

        // Reconcile with stored issues.
        var existing = await _db.DataQualityIssues.Where(i => i.EntityId != null).ToListAsync(ct);
        var created = 0;
        foreach (var d in detected)
        {
            var match = existing.FirstOrDefault(i => i.RuleCode == d.RuleCode && i.EntityId == d.EntityId);
            if (match is not null) continue; // already open, or deliberately closed/ignored by its owner
            _db.DataQualityIssues.Add(new DataQualityIssue
            {
                RuleCode = d.RuleCode,
                Description = d.Description.Length > 1000 ? d.Description[..1000] : d.Description,
                EntityType = d.EntityType,
                EntityId = d.EntityId,
                EntityReference = d.Reference,
                Severity = d.Severity,
                OwnerUserId = d.OwnerUserId,
                OwnerName = d.OwnerName,
                DetectedAtUtc = _clock.UtcNow
            });
            created++;
        }
        var stillDetected = detected.Select(d => (d.RuleCode, d.EntityId)).ToHashSet();
        foreach (var open in existing.Where(i => i.Status == DataQualityStatus.Open && !stillDetected.Contains((i.RuleCode, i.EntityId!.Value))))
        {
            open.Status = DataQualityStatus.Resolved;
            open.Resolution = "Auto-resolved: the condition is no longer detected.";
            open.ResolvedAtUtc = _clock.UtcNow;
        }
        // Allow a resolved issue to be raised again if the condition re-occurs later.
        foreach (var resolved in existing.Where(i => i.Status == DataQualityStatus.Resolved && stillDetected.Contains((i.RuleCode, i.EntityId!.Value))
                                                     && i.Resolution?.StartsWith("Auto-resolved", StringComparison.Ordinal) == true))
        {
            resolved.Status = DataQualityStatus.Open;
            resolved.Resolution = null;
            resolved.ResolvedAtUtc = null;
        }
        await _db.SaveChangesAsync(ct);
        return created;
    }
}
