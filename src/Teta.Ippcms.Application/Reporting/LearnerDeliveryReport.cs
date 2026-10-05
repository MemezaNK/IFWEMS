using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Budget;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Documents;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Monitoring;
using Teta.Ippcms.Domain.Projects;

namespace Teta.Ippcms.Application.Reporting;

// ---------- DTOs ----------

/// <summary>A single executive KPI row: contract/target vs actual, with achievement % and RAG status.
/// Status is "NotConfigured" when no <see cref="LearnerDeliveryTarget"/> row exists for the project.</summary>
public sealed record LearnerDeliveryKpiDto(string Name, decimal? Target, decimal Actual, decimal? AchievementPercent, string Status);

public sealed record CohortDeliveryDto(string Cohort, int Registered, int Commenced, int Active, int Completed, int Withdrawn,
    decimal CompletionPercent, string Status);

/// <summary>Aggregated (not individually identifying) learner register row. No learner name/ID is exposed here;
/// full identity reveal remains gated by <see cref="Teta.Ippcms.Domain.Security.Permissions.BeneficiaryPii"/> on the
/// existing Beneficiary endpoints, per the report's own privacy instruction.</summary>
public sealed record LearnerRegisterItemDto(Guid Id, string Number, string? Cohort, DateTime RegisteredAtUtc, string Status,
    string Progress, bool EvidenceVerified, string? Exception);

public sealed record LearnerExceptionDto(string Category, int LearnerCount, string Impact, string ResponsibleParty, string RequiredAction, string Status);

public sealed record DeliverablePerformanceDto(string Name, DateOnly Planned, DateTime? Actual, int? VarianceDays, bool EvidenceRequired, string Status);

public sealed record MonitoringVisitItemDto(string Number, string Type, DateOnly PlannedDate, DateOnly? ActualDate, string? Scope, string Status);

public sealed record FindingItemDto(string Number, string Description, string Severity, DateTime? ClosedAtUtc, string Status);

public sealed record CorrectiveActionItemDto(string Number, string Description, string OwnerName, DateOnly DueDate, string Status, bool Overdue);

public sealed record EvidenceCategoryDto(string Category, int Total, int Verified, int Pending, int Rejected, decimal CompletenessPercent);

public sealed record FinancialAlignmentDto(decimal ApprovedBudget, decimal Committed, decimal ActualExpenditure, decimal RemainingBudget,
    decimal ForecastFinalCost, decimal ForecastVarianceVsBudget);

public sealed record PaymentMilestoneItemDto(string Description, decimal Amount, DateOnly PlannedDate, string DeliveryStatus, string PaymentStatus);

public sealed record ProviderScorecardItemDto(string Area, decimal Score, decimal MaxScore, string Rating);

public sealed record RiskItemDto(string Number, string Title, string Rating, string Status, string OwnerName, DateOnly? NextActionDueDate);

public sealed record LearnerDeliveryTargetDto(Guid ProjectId, int ContractedLearners, int LearnersDueForCompletion, int MonitoringVisitsPlanned,
    int WithdrawalTolerancePercent, bool IsConfigured);

public sealed record SaveLearnerDeliveryTargetRequest(int ContractedLearners, int LearnersDueForCompletion, int MonitoringVisitsPlanned,
    int WithdrawalTolerancePercent);

/// <summary>
/// The full "Learner Delivery &amp; Monitoring Report" dataset (sections A-M of the TETA-IPPCMS spec),
/// shaped for both the interactive dashboard and the consolidated PDF/XLSX export.
/// </summary>
public sealed record LearnerDeliveryDashboardDto(
    Guid ProjectId, string ProjectReference, string ProjectName, string? ProgrammeName, string? TrainingProvider,
    string ProjectManagerName, string MeOfficerName, DateOnly? ContractStart, DateOnly? ContractEnd, decimal? ContractValue,
    string OverallStatus, DateTime GeneratedAtUtc,
    IReadOnlyList<LearnerDeliveryKpiDto> ExecutiveSummary,
    IReadOnlyList<CohortDeliveryDto> Cohorts,
    IReadOnlyList<LearnerRegisterItemDto> LearnerRegister,
    IReadOnlyList<LearnerExceptionDto> Exceptions,
    IReadOnlyList<DeliverablePerformanceDto> DeliveryPerformance,
    IReadOnlyList<MonitoringVisitItemDto> MonitoringVisits,
    IReadOnlyList<FindingItemDto> Findings,
    IReadOnlyList<CorrectiveActionItemDto> CorrectiveActions,
    IReadOnlyList<EvidenceCategoryDto> Evidence,
    FinancialAlignmentDto Financial,
    IReadOnlyList<PaymentMilestoneItemDto> PaymentMilestones,
    IReadOnlyList<ProviderScorecardItemDto> ProviderScorecard,
    IReadOnlyList<RiskItemDto> Risks,
    IReadOnlyList<string> ManagementDecisions);

public interface ILearnerDeliveryReportService
{
    Task<LearnerDeliveryDashboardDto> GetDashboardAsync(Guid projectId, CancellationToken ct);
    Task<LearnerDeliveryTargetDto> GetTargetAsync(Guid projectId, CancellationToken ct);
    Task<LearnerDeliveryTargetDto> SaveTargetAsync(Guid projectId, SaveLearnerDeliveryTargetRequest request, CancellationToken ct);
    Task<ExportedFile> ExportAsync(Guid projectId, string format, CancellationToken ct);
}

/// <summary>
/// Computes the Learner Delivery &amp; Monitoring Report (learner/M&amp;E/contract/financial/risk roll-up for a
/// single project) and exports it as a consolidated PDF or multi-sheet XLSX. RAG thresholds below are sensible
/// defaults only - the spec explicitly requires TETA to validate/approve final KPI thresholds and scorecard
/// weights before they are relied on operationally.
/// </summary>
public sealed class LearnerDeliveryReportService : ILearnerDeliveryReportService
{
    private readonly ITetaDbContext _db;
    private readonly IAccessScope _scope;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public LearnerDeliveryReportService(ITetaDbContext db, IAccessScope scope, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _scope = scope;
        _clock = clock;
        _audit = audit;
    }

    public async Task<LearnerDeliveryTargetDto> GetTargetAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var target = await _db.LearnerDeliveryTargets.AsNoTracking().FirstOrDefaultAsync(t => t.ProjectId == projectId, ct);
        return ToDto(projectId, target);
    }

    public async Task<LearnerDeliveryTargetDto> SaveTargetAsync(Guid projectId, SaveLearnerDeliveryTargetRequest request, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator()
            .Must(request.ContractedLearners >= 0, nameof(request.ContractedLearners), "Contracted learners cannot be negative.")
            .Must(request.LearnersDueForCompletion >= 0, nameof(request.LearnersDueForCompletion), "Learners due for completion cannot be negative.")
            .Must(request.MonitoringVisitsPlanned >= 0, nameof(request.MonitoringVisitsPlanned), "Monitoring visits planned cannot be negative.")
            .Range(nameof(request.WithdrawalTolerancePercent), request.WithdrawalTolerancePercent, 0, 100)
            .ThrowIfInvalid();

        var target = await _db.LearnerDeliveryTargets.FirstOrDefaultAsync(t => t.ProjectId == projectId, ct);
        if (target is null)
        {
            target = new LearnerDeliveryTarget { ProjectId = projectId };
            _db.LearnerDeliveryTargets.Add(target);
        }
        target.ContractedLearners = request.ContractedLearners;
        target.LearnersDueForCompletion = request.LearnersDueForCompletion;
        target.MonitoringVisitsPlanned = request.MonitoringVisitsPlanned;
        target.WithdrawalTolerancePercent = request.WithdrawalTolerancePercent;
        await _db.SaveChangesAsync(ct);

        _audit.Write("Reporting", nameof(LearnerDeliveryTarget), projectId.ToString(), "TargetsConfigured", request);
        return ToDto(projectId, target);
    }

    public async Task<LearnerDeliveryDashboardDto> GetDashboardAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var today = _clock.Today;

        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new NotFoundException("Project", projectId);
        var programme = await _db.Programmes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == project.ProgrammeId, ct);
        var target = await _db.LearnerDeliveryTargets.AsNoTracking().FirstOrDefaultAsync(t => t.ProjectId == projectId, ct);

        var beneficiaries = await _db.Beneficiaries.AsNoTracking().Where(b => b.ProjectId == projectId).ToListAsync(ct);
        var visits = await _db.MonitoringVisits.AsNoTracking().Where(v => v.ProjectId == projectId).OrderBy(v => v.ScheduledDate).ToListAsync(ct);
        var findings = await _db.Findings.AsNoTracking().Where(f => f.ProjectId == projectId).ToListAsync(ct);
        var correctiveActions = await _db.CorrectiveActions.AsNoTracking().Where(a => a.ProjectId == projectId).ToListAsync(ct);
        var evidence = await _db.Evidence.AsNoTracking().Where(e => e.ProjectId == projectId).ToListAsync(ct);

        var contract = await _db.Contracts.AsNoTracking().Where(c => c.ProjectId == projectId)
            .OrderByDescending(c => c.Status == ContractStatus.Active).ThenByDescending(c => c.StartDate).FirstOrDefaultAsync(ct);
        var supplier = contract is null ? null : await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == contract.SupplierId, ct);
        var deliverables = await _db.Deliverables.AsNoTracking().Where(d => d.ProjectId == projectId).ToListAsync(ct);
        var paymentItems = contract is null
            ? new List<PaymentScheduleItem>()
            : await _db.PaymentScheduleItems.AsNoTracking().Where(p => p.ContractId == contract.Id).OrderBy(p => p.PlannedDate).ToListAsync(ct);
        var performanceReview = contract is null
            ? null
            : await _db.ContractPerformanceReviews.AsNoTracking().Where(r => r.ContractId == contract.Id)
                .OrderByDescending(r => r.ReviewDate).FirstOrDefaultAsync(ct);

        var budgetLines = await _db.BudgetLines.AsNoTracking().Where(b => b.ProjectId == projectId).ToListAsync(ct);
        var commitments = await _db.Commitments.AsNoTracking().Where(c => c.ProjectId == projectId && !c.IsReleased).ToListAsync(ct);
        var expenditures = await _db.Expenditures.AsNoTracking().Where(e => e.ProjectId == projectId).ToListAsync(ct);

        var risks = await _db.Risks.AsNoTracking().Where(r => r.ProjectId == projectId && (r.Status == RiskStatus.Open || r.Status == RiskStatus.Treating)).ToListAsync(ct);
        var riskIds = risks.Select(r => r.Id).ToList();
        var riskTreatments = riskIds.Count == 0
            ? new List<RiskTreatment>()
            : await _db.RiskTreatments.AsNoTracking().Where(t => riskIds.Contains(t.RiskId) && t.Status != ActionStatus.Completed && t.Status != ActionStatus.Cancelled)
                .ToListAsync(ct);

        var executiveSummary = BuildExecutiveSummary(target, beneficiaries, visits, findings, correctiveActions, evidence, today);
        var cohorts = BuildCohorts(beneficiaries);
        var learnerRegister = BuildLearnerRegister(beneficiaries, evidence, today);
        var exceptions = BuildExceptions(beneficiaries, evidence, today);
        var deliveryPerformance = BuildDeliveryPerformance(deliverables);
        var visitItems = visits.Select(v => new MonitoringVisitItemDto(v.Number, v.Type.ToString(), v.ScheduledDate, v.VisitDate, v.Summary,
            v.Status.ToString())).ToList();
        var findingItems = findings.Select(f => new FindingItemDto(f.Number, f.Description, f.Severity.ToString(), f.ClosedAtUtc, f.Status.ToString()))
            .ToList();
        var correctiveActionItems = correctiveActions.Select(a => new CorrectiveActionItemDto(a.Number, a.Description, a.OwnerName, a.DueDate,
            a.Status.ToString(), a.IsOverdue(today))).ToList();
        var evidenceCategories = BuildEvidenceCategories(evidence);
        var financial = BuildFinancial(project, budgetLines, commitments, expenditures);
        var paymentMilestones = BuildPaymentMilestones(paymentItems, deliverables);
        var scorecard = BuildScorecard(performanceReview);
        var riskItems = BuildRisks(risks, riskTreatments);

        var overdueActions = correctiveActionItems.Count(a => a.Overdue);
        var overallStatus = Rag(executiveSummary.Where(k => k.Status != "NotConfigured").Select(k => k.Status).Append(overdueActions > 0 ? "Red" : "Green"));
        var decisions = BuildManagementDecisions(executiveSummary, exceptions, correctiveActionItems, evidenceCategories, financial, scorecard, riskItems);

        return new LearnerDeliveryDashboardDto(
            project.Id, project.Reference, project.Name, programme?.Name, supplier?.LegalName,
            project.ManagerName ?? "Not assigned", "Not assigned",
            contract?.StartDate, contract?.CurrentEndDate, contract?.RevisedValue,
            overallStatus, _clock.UtcNow,
            executiveSummary, cohorts, learnerRegister, exceptions, deliveryPerformance, visitItems, findingItems, correctiveActionItems,
            evidenceCategories, financial, paymentMilestones, scorecard, riskItems, decisions);
    }

    public async Task<ExportedFile> ExportAsync(Guid projectId, string format, CancellationToken ct)
    {
        var dashboard = await GetDashboardAsync(projectId, ct);
        var fileBase = $"Learner-Delivery-Report-{dashboard.ProjectReference}";

        ExportedFile file = format.ToLowerInvariant() switch
        {
            "xlsx" => new ExportedFile(ReportExport.XlsxMultiSheet(BuildSheets(dashboard)),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileBase}.xlsx"),
            "pdf" => new ExportedFile(BuildPdf(dashboard), "application/pdf", $"{fileBase}.pdf"),
            _ => throw new ValidationException("format", "Supported export formats are 'pdf' and 'xlsx'.")
        };

        _audit.Write("Reporting", "LearnerDeliveryReport", projectId.ToString(), "Export", new { format });
        return file;
    }

    // ---------------- Section builders ----------------

    private static IReadOnlyList<LearnerDeliveryKpiDto> BuildExecutiveSummary(LearnerDeliveryTarget? target, List<Beneficiary> beneficiaries,
        List<MonitoringVisit> visits, List<Finding> findings, List<CorrectiveAction> correctiveActions, List<Evidence> evidence, DateOnly today)
    {
        var commenced = beneficiaries.Count(b => b.Status is BeneficiaryStatus.Participating or BeneficiaryStatus.Completed or BeneficiaryStatus.Placed
            or BeneficiaryStatus.DroppedOut);
        var active = beneficiaries.Count(b => b.Status == BeneficiaryStatus.Participating);
        var completed = beneficiaries.Count(b => b.Status is BeneficiaryStatus.Completed or BeneficiaryStatus.Placed);
        var withdrawn = beneficiaries.Count(b => b.Status == BeneficiaryStatus.DroppedOut);
        var verifiedLearners = evidence.Where(e => e.ParentType == ParentTypes.Beneficiary && e.VerificationStatus == EvidenceStatus.Verified)
            .Select(e => e.ParentId).Distinct().Count();
        var visitsCompleted = visits.Count(v => v.Status == VisitStatus.Completed);
        var findingsClosed = findings.Count(f => f.Status == FindingStatus.Closed);
        var overdueActions = correctiveActions.Count(a => a.IsOverdue(today));

        var list = new List<LearnerDeliveryKpiDto>
        {
            Kpi("Contracted Learners", target?.ContractedLearners, beneficiaries.Count),
            Kpi("Learners Registered", target?.ContractedLearners, beneficiaries.Count),
            KpiNoTarget("Learners Commenced", commenced),
            KpiNoTarget("Active Learners", active),
            Kpi("Learners Due for Completion", target?.LearnersDueForCompletion, completed),
            KpiNoTarget("Learners Completed", completed),
            KpiInverse("Learners Withdrawn", withdrawn, beneficiaries.Count, target?.WithdrawalTolerancePercent),
            KpiNoTarget("Learner Evidence Complete (%)", beneficiaries.Count == 0 ? 0 : Math.Round(100m * verifiedLearners / beneficiaries.Count, 1)),
            Kpi("Monitoring Visits Planned/Completed", target?.MonitoringVisitsPlanned, visitsCompleted),
            KpiNoTarget("Findings Raised", findings.Count),
            KpiNoTarget("Findings Closed", findingsClosed),
            KpiInverseCount("Corrective Actions Overdue", overdueActions)
        };
        return list;
    }

    private static List<CohortDeliveryDto> BuildCohorts(List<Beneficiary> beneficiaries) =>
        beneficiaries.GroupBy(b => string.IsNullOrWhiteSpace(b.Cohort) ? "Unassigned" : b.Cohort!)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var registered = g.Count();
                var commenced = g.Count(b => b.Status is BeneficiaryStatus.Participating or BeneficiaryStatus.Completed or BeneficiaryStatus.Placed
                    or BeneficiaryStatus.DroppedOut);
                var active = g.Count(b => b.Status == BeneficiaryStatus.Participating);
                var completed = g.Count(b => b.Status is BeneficiaryStatus.Completed or BeneficiaryStatus.Placed);
                var withdrawn = g.Count(b => b.Status == BeneficiaryStatus.DroppedOut);
                var completionPercent = registered == 0 ? 0 : Math.Round(100m * completed / registered, 1);
                return new CohortDeliveryDto(g.Key, registered, commenced, active, completed, withdrawn, completionPercent,
                    RagAchievement(completionPercent));
            }).ToList();

    private static List<LearnerRegisterItemDto> BuildLearnerRegister(List<Beneficiary> beneficiaries, List<Evidence> evidence, DateOnly today) =>
        beneficiaries.Select(b =>
        {
            var hasVerifiedEvidence = evidence.Any(e => e.ParentType == ParentTypes.Beneficiary && e.ParentId == b.Id
                && e.VerificationStatus == EvidenceStatus.Verified);
            string? exception = b.PotentialDuplicate ? "Potential duplicate record"
                : b.Status == BeneficiaryStatus.Registered && today.DayNumber - DateOnly.FromDateTime(b.CreatedAtUtc).DayNumber > 30
                    ? "Registration outstanding"
                : b.Status == BeneficiaryStatus.DroppedOut ? "Withdrawn"
                : !hasVerifiedEvidence && b.Status != BeneficiaryStatus.Registered ? "Missing verified evidence"
                : null;
            var progress = b.Status switch
            {
                BeneficiaryStatus.Registered => "0%",
                BeneficiaryStatus.Enrolled => "10%",
                BeneficiaryStatus.Participating => "50%",
                BeneficiaryStatus.Completed or BeneficiaryStatus.Placed => "100%",
                BeneficiaryStatus.DroppedOut => "Withdrawn",
                _ => "-"
            };
            return new LearnerRegisterItemDto(b.Id, b.Number, b.Cohort, b.CreatedAtUtc, b.Status.ToString(), progress, hasVerifiedEvidence, exception);
        }).OrderBy(x => x.Number).ToList();

    private static List<LearnerExceptionDto> BuildExceptions(List<Beneficiary> beneficiaries, List<Evidence> evidence, DateOnly today)
    {
        int DaysSince(DateTime utc) => today.DayNumber - DateOnly.FromDateTime(utc).DayNumber;
        var registrationOutstanding = beneficiaries.Count(b => b.Status == BeneficiaryStatus.Registered && DaysSince(b.CreatedAtUtc) > 30);
        var didNotCommence = beneficiaries.Count(b => b.Status == BeneficiaryStatus.Enrolled && DaysSince(b.CreatedAtUtc) > 60);
        var withdrawals = beneficiaries.Count(b => b.Status == BeneficiaryStatus.DroppedOut);
        var missingEvidence = beneficiaries.Count(b => b.Status != BeneficiaryStatus.Registered
            && !evidence.Any(e => e.ParentType == ParentTypes.Beneficiary && e.ParentId == b.Id && e.VerificationStatus == EvidenceStatus.Verified));
        var duplicates = beneficiaries.Count(b => b.PotentialDuplicate);

        return new List<LearnerExceptionDto>
        {
            new("Registration outstanding", registrationOutstanding, "Delays enrolment and cohort start",
                "M&E Officer", "Follow up with training provider to finalise registration", RagInverseCount(registrationOutstanding)),
            new("Did not commence", didNotCommence, "Learner enrolled but has not started training",
                "Training Provider", "Confirm learner availability or substitute learner", RagInverseCount(didNotCommence)),
            new("Behind programme schedule", 0, "Not currently tracked", "Not tracked",
                "Requires manual review - no linked schedule data source", "NotConfigured"),
            new("Withdrawals", withdrawals, "Reduces completion rate against contract", "Training Provider",
                "Confirm withdrawal reason and consider substitute learner", RagInverseCount(withdrawals)),
            new("Missing evidence", missingEvidence, "Blocks payment certification and audit assurance",
                "Training Provider", "Submit outstanding POE/evidence for verification", RagInverseCount(missingEvidence)),
            new("Assessment outstanding", 0, "Not currently tracked", "Not tracked",
                "Requires manual review - no linked assessment data source", "NotConfigured"),
            new("Potential duplicate records", duplicates, "Risk of double-funding/double-counting",
                "M&E Officer", "Investigate and resolve duplicate flag", RagInverseCount(duplicates))
        };
    }

    private static List<DeliverablePerformanceDto> BuildDeliveryPerformance(List<Deliverable> deliverables) =>
        deliverables.OrderBy(d => d.DueDate).Select(d =>
        {
            int? variance = d.AcceptedAtUtc is { } accepted ? DateOnly.FromDateTime(accepted).DayNumber - d.DueDate.DayNumber : null;
            var status = d.AcceptanceStatus switch
            {
                AcceptanceStatus.Accepted => "Green",
                AcceptanceStatus.Rejected => "Red",
                AcceptanceStatus.Submitted => "Amber",
                _ => "Amber"
            };
            return new DeliverablePerformanceDto(d.Name, d.DueDate, d.AcceptedAtUtc, variance, d.EvidenceRequired, status);
        }).ToList();

    private static List<EvidenceCategoryDto> BuildEvidenceCategories(List<Evidence> evidence) =>
        evidence.GroupBy(e => e.EvidenceType).OrderBy(g => g.Key).Select(g =>
        {
            var total = g.Count();
            var verified = g.Count(e => e.VerificationStatus == EvidenceStatus.Verified);
            var pending = g.Count(e => e.VerificationStatus == EvidenceStatus.Pending);
            var rejected = g.Count(e => e.VerificationStatus == EvidenceStatus.Rejected);
            return new EvidenceCategoryDto(g.Key, total, verified, pending, rejected, total == 0 ? 0 : Math.Round(100m * verified / total, 1));
        }).ToList();

    private static FinancialAlignmentDto BuildFinancial(Project project, List<ProjectBudgetLine> budgetLines, List<Commitment> commitments,
        List<Expenditure> expenditures)
    {
        var approved = budgetLines.Count == 0 ? project.ApprovedBudget : budgetLines.Sum(b => b.RevisedAmount);
        var committed = commitments.Sum(c => c.Amount);
        var actual = expenditures.Sum(e => e.Amount);
        var forecast = budgetLines.Count == 0 ? actual + committed : budgetLines.Sum(b => b.ForecastAmount);
        var remaining = approved - actual - committed;
        return new FinancialAlignmentDto(approved, committed, actual, remaining, forecast, approved - forecast);
    }

    private static List<PaymentMilestoneItemDto> BuildPaymentMilestones(List<PaymentScheduleItem> paymentItems, List<Deliverable> deliverables) =>
        paymentItems.Select(p =>
        {
            var deliverable = p.DeliverableId is { } id ? deliverables.FirstOrDefault(d => d.Id == id) : null;
            var deliveryStatus = deliverable?.AcceptanceStatus.ToString() ?? "No linked deliverable";
            var paymentStatus = deliverable?.AcceptanceStatus == AcceptanceStatus.Accepted ? "Certified for payment" : "Pending certification";
            return new PaymentMilestoneItemDto(p.Description, p.Amount, p.PlannedDate, deliveryStatus, paymentStatus);
        }).OrderBy(p => p.PlannedDate).ToList();

    private static List<ProviderScorecardItemDto> BuildScorecard(ContractPerformanceReview? review)
    {
        if (review is null)
        {
            return new List<ProviderScorecardItemDto> { new("Overall", 0, 5, "Not yet reviewed") };
        }
        return new List<ProviderScorecardItemDto>
        {
            new("Training Delivery Quality", review.QualityScore, 5, RagScore(review.QualityScore)),
            new("Timeliness", review.TimelinessScore, 5, RagScore(review.TimelinessScore)),
            new("Compliance", review.ComplianceScore, 5, RagScore(review.ComplianceScore)),
            new("Overall", review.OverallScore, 5, review.Rating)
        };
    }

    private static List<RiskItemDto> BuildRisks(List<Risk> risks, List<RiskTreatment> treatments) =>
        risks.Select(r =>
        {
            var nextDue = treatments.Where(t => t.RiskId == r.Id).OrderBy(t => t.DueDate).Select(t => (DateOnly?)t.DueDate).FirstOrDefault();
            return new RiskItemDto(r.Number, r.Title, r.ResidualRating, r.Status.ToString(), r.OwnerName, nextDue);
        }).ToList();

    private static List<string> BuildManagementDecisions(IReadOnlyList<LearnerDeliveryKpiDto> kpis, List<LearnerExceptionDto> exceptions,
        List<CorrectiveActionItemDto> correctiveActions, List<EvidenceCategoryDto> evidenceCategories, FinancialAlignmentDto financial,
        List<ProviderScorecardItemDto> scorecard, List<RiskItemDto> risks)
    {
        var decisions = new List<string>();
        var overdue = correctiveActions.Count(a => a.Overdue);
        if (overdue > 0)
            decisions.Add($"Escalate {overdue} overdue corrective action(s) with the training provider and set a recovery deadline.");

        var withdrawalKpi = kpis.FirstOrDefault(k => k.Name == "Learners Withdrawn");
        if (withdrawalKpi is { Status: "Red" or "Amber" })
            decisions.Add("Review withdrawal rate against contract tolerance; consider a provider recovery plan if trend continues.");

        var evidenceAvg = evidenceCategories.Count == 0 ? 100m : evidenceCategories.Average(e => e.CompletenessPercent);
        if (evidenceAvg < 80)
            decisions.Add("Require the training provider to remediate outstanding learner evidence before certifying further payments.");

        if (financial.ForecastVarianceVsBudget < 0)
            decisions.Add("Forecast final cost exceeds approved budget - review scope, variations or funding options.");

        var overallScore = scorecard.FirstOrDefault(s => s.Area == "Overall");
        if (overallScore is { Score: < 3 })
            decisions.Add("Provider performance scorecard is below acceptable threshold - consider a formal performance improvement plan.");

        if (risks.Any(r => r.Rating is "Critical" or "High"))
            decisions.Add("Review high/critical residual risks and confirm treatment actions are adequately resourced.");

        if (exceptions.Any(e => e.Status == "Red"))
            decisions.Add("Address Red-rated learner exceptions (registration, withdrawals, missing evidence) before the next reporting cycle.");

        if (decisions.Count == 0) decisions.Add("No management decisions required this period - delivery is tracking within configured thresholds.");
        return decisions;
    }

    // ---------------- Export shaping ----------------

    private static List<ReportTable> BuildSheets(LearnerDeliveryDashboardDto d)
    {
        var now = d.GeneratedAtUtc;
        ReportTable Table(string code, string title, string[] columns, IEnumerable<object?[]> rows) =>
            new(code, title, now, columns, rows.ToList(), new Dictionary<string, string?> { ["Project"] = d.ProjectReference });

        return new List<ReportTable>
        {
            Table("EXEC", "Executive Summary", new[] { "KPI", "Target", "Actual", "Achievement %", "Status" },
                d.ExecutiveSummary.Select(k => new object?[] { k.Name, k.Target, k.Actual, k.AchievementPercent, k.Status })),
            Table("COHORT", "Learner Delivery by Cohort",
                new[] { "Cohort", "Registered", "Commenced", "Active", "Completed", "Withdrawn", "Completion %", "Status" },
                d.Cohorts.Select(c => new object?[] { c.Cohort, c.Registered, c.Commenced, c.Active, c.Completed, c.Withdrawn, c.CompletionPercent, c.Status })),
            Table("REGISTER", "Learner Status Register",
                new[] { "Learner Ref", "Cohort", "Registered", "Status", "Progress", "Evidence Verified", "Exception" },
                d.LearnerRegister.Select(l => new object?[] { l.Number, l.Cohort, l.RegisteredAtUtc, l.Status, l.Progress, l.EvidenceVerified, l.Exception })),
            Table("EXCEPT", "Learner Exceptions", new[] { "Category", "Learners", "Impact", "Responsible Party", "Required Action", "Status" },
                d.Exceptions.Select(e => new object?[] { e.Category, e.LearnerCount, e.Impact, e.ResponsibleParty, e.RequiredAction, e.Status })),
            Table("DELIVERY", "Training Delivery Performance", new[] { "Deliverable", "Planned", "Actual", "Variance (days)", "Evidence Required", "Status" },
                d.DeliveryPerformance.Select(p => new object?[] { p.Name, p.Planned, p.Actual, p.VarianceDays, p.EvidenceRequired, p.Status })),
            Table("VISITS", "Monitoring Visit Programme", new[] { "Visit", "Type", "Planned", "Actual", "Scope", "Status" },
                d.MonitoringVisits.Select(v => new object?[] { v.Number, v.Type, v.PlannedDate, v.ActualDate, v.Scope, v.Status })),
            Table("FINDINGS", "Monitoring Findings Register", new[] { "Finding", "Description", "Severity", "Closed", "Status" },
                d.Findings.Select(f => new object?[] { f.Number, f.Description, f.Severity, f.ClosedAtUtc, f.Status })),
            Table("ACTIONS", "Corrective Action Tracking", new[] { "Action", "Description", "Owner", "Due Date", "Status", "Overdue" },
                d.CorrectiveActions.Select(a => new object?[] { a.Number, a.Description, a.OwnerName, a.DueDate, a.Status, a.Overdue })),
            Table("EVIDENCE", "Evidence Completeness", new[] { "Category", "Total", "Verified", "Pending", "Rejected", "Completeness %" },
                d.Evidence.Select(e => new object?[] { e.Category, e.Total, e.Verified, e.Pending, e.Rejected, e.CompletenessPercent })),
            Table("FINANCE", "Financial Delivery Alignment",
                new[] { "Approved Budget", "Committed", "Actual Expenditure", "Remaining Budget", "Forecast Final Cost", "Forecast Variance" },
                new[] { new object?[] { d.Financial.ApprovedBudget, d.Financial.Committed, d.Financial.ActualExpenditure, d.Financial.RemainingBudget,
                    d.Financial.ForecastFinalCost, d.Financial.ForecastVarianceVsBudget } }),
            Table("PAYMENTS", "Payment Milestone Control", new[] { "Milestone", "Amount", "Planned Date", "Delivery Status", "Payment Status" },
                d.PaymentMilestones.Select(p => new object?[] { p.Description, p.Amount, p.PlannedDate, p.DeliveryStatus, p.PaymentStatus })),
            Table("SCORECARD", "Provider Performance Scorecard", new[] { "Area", "Score", "Max", "Rating" },
                d.ProviderScorecard.Select(s => new object?[] { s.Area, s.Score, s.MaxScore, s.Rating })),
            Table("RISKS", "Risks and Management Actions", new[] { "Risk", "Title", "Residual Rating", "Status", "Owner", "Next Action Due" },
                d.Risks.Select(r => new object?[] { r.Number, r.Title, r.Rating, r.Status, r.OwnerName, r.NextActionDueDate })),
            Table("DECISIONS", "Management Decisions Required", new[] { "Decision" },
                d.ManagementDecisions.Select(m => new object?[] { m }))
        };
    }

    private static byte[] BuildPdf(LearnerDeliveryDashboardDto d)
    {
        var header = new List<(string Label, string? Value)>
        {
            ("Project", d.ProjectReference), ("Project Name", d.ProjectName), ("Programme", d.ProgrammeName),
            ("Training Provider", d.TrainingProvider), ("Project Manager", d.ProjectManagerName),
            ("Contract Period", d.ContractStart is null ? null : $"{d.ContractStart:yyyy-MM-dd} to {d.ContractEnd:yyyy-MM-dd}"),
            ("Contract Value", d.ContractValue?.ToString("N2")), ("Overall Status", d.OverallStatus)
        };

        var sections = new List<(string Heading, IReadOnlyList<string> Columns, IReadOnlyList<object?[]> Rows)>
        {
            ("A. Executive Delivery Summary", new[] { "KPI", "Target", "Actual", "Achv %", "Status" },
                d.ExecutiveSummary.Select(k => new object?[] { k.Name, k.Target, k.Actual, k.AchievementPercent, k.Status }).ToList()),
            ("B. Learner Delivery by Cohort", new[] { "Cohort", "Reg", "Commenced", "Active", "Completed", "Withdrawn", "Compl %", "Status" },
                d.Cohorts.Select(c => new object?[] { c.Cohort, c.Registered, c.Commenced, c.Active, c.Completed, c.Withdrawn, c.CompletionPercent, c.Status })
                    .ToList()),
            ("D. Learner Exceptions", new[] { "Category", "Learners", "Impact", "Responsible", "Required Action", "Status" },
                d.Exceptions.Select(e => new object?[] { e.Category, e.LearnerCount, e.Impact, e.ResponsibleParty, e.RequiredAction, e.Status }).ToList()),
            ("E. Training Delivery Performance", new[] { "Deliverable", "Planned", "Actual", "Variance", "Status" },
                d.DeliveryPerformance.Select(p => new object?[] { p.Name, p.Planned, p.Actual, p.VarianceDays, p.Status }).ToList()),
            ("F. Monitoring Visit Programme", new[] { "Visit", "Type", "Planned", "Actual", "Status" },
                d.MonitoringVisits.Select(v => new object?[] { v.Number, v.Type, v.PlannedDate, v.ActualDate, v.Status }).ToList()),
            ("G. Monitoring Findings Register", new[] { "Finding", "Description", "Severity", "Status" },
                d.Findings.Select(f => new object?[] { f.Number, f.Description, f.Severity, f.Status }).ToList()),
            ("H. Corrective Action Tracking", new[] { "Action", "Description", "Owner", "Due", "Status" },
                d.CorrectiveActions.Select(a => new object?[] { a.Number, a.Description, a.OwnerName, a.DueDate, a.Overdue ? "Overdue" : a.Status.ToString() })
                    .ToList()),
            ("I. Evidence Completeness", new[] { "Category", "Total", "Verified", "Completeness %" },
                d.Evidence.Select(e => new object?[] { e.Category, e.Total, e.Verified, e.CompletenessPercent }).ToList()),
            ("J. Financial Delivery Alignment", new[] { "Approved", "Committed", "Actual", "Remaining", "Forecast", "Variance" },
                new List<object?[]> { new object?[] { d.Financial.ApprovedBudget, d.Financial.Committed, d.Financial.ActualExpenditure,
                    d.Financial.RemainingBudget, d.Financial.ForecastFinalCost, d.Financial.ForecastVarianceVsBudget } }),
            ("K. Payment Milestone Control", new[] { "Milestone", "Amount", "Planned", "Delivery Status", "Payment" },
                d.PaymentMilestones.Select(p => new object?[] { p.Description, p.Amount, p.PlannedDate, p.DeliveryStatus, p.PaymentStatus }).ToList()),
            ("L. Provider Performance Scorecard", new[] { "Area", "Score", "Max", "Rating" },
                d.ProviderScorecard.Select(s => new object?[] { s.Area, s.Score, s.MaxScore, s.Rating }).ToList()),
            ("M. Risks and Management Actions", new[] { "Risk", "Title", "Rating", "Status", "Owner", "Due" },
                d.Risks.Select(r => new object?[] { r.Number, r.Title, r.Rating, r.Status, r.OwnerName, r.NextActionDueDate }).ToList()),
            ("N. Management Decisions Required", Array.Empty<string>(), new List<object?[]>())
        };

        // "N" is narrative text rather than a table; append as pseudo-rows with no columns is awkward, so
        // fold it into its own single-column section instead.
        sections[^1] = ("N. Management Decisions Required", new[] { "Decision" },
            d.ManagementDecisions.Select(m => new object?[] { m }).ToList());

        return ReportExport.PdfSections($"Learner Delivery & Monitoring Report - {d.ProjectName}", d.GeneratedAtUtc, header, sections);
    }

    // ---------------- RAG helpers ----------------

    private static LearnerDeliveryKpiDto Kpi(string name, int? target, decimal actual)
    {
        if (target is not { } t || t == 0) return new LearnerDeliveryKpiDto(name, target, actual, null, "NotConfigured");
        var pct = Math.Round(100m * actual / t, 1);
        return new LearnerDeliveryKpiDto(name, t, actual, pct, RagAchievement(pct));
    }

    private static LearnerDeliveryKpiDto KpiNoTarget(string name, decimal actual) => new(name, null, actual, null, "NotConfigured");

    private static LearnerDeliveryKpiDto KpiInverse(string name, int actual, int total, int? tolerancePercent)
    {
        if (tolerancePercent is not { } tol) return new LearnerDeliveryKpiDto(name, null, actual, null, "NotConfigured");
        var actualPercent = total == 0 ? 0 : Math.Round(100m * actual / total, 1);
        var status = actualPercent <= tol ? "Green" : actualPercent <= tol * 1.5m ? "Amber" : "Red";
        return new LearnerDeliveryKpiDto(name, tol, actualPercent, actualPercent, status);
    }

    private static LearnerDeliveryKpiDto KpiInverseCount(string name, int count) =>
        new(name, 0, count, null, count == 0 ? "Green" : count <= 2 ? "Amber" : "Red");

    private static string RagAchievement(decimal achievementPercent) => achievementPercent switch
    {
        >= 95 => "Green",
        >= 80 => "Amber",
        _ => "Red"
    };

    private static string RagScore(decimal scoreOutOf5) => scoreOutOf5 switch
    {
        >= 4 => "Green",
        >= 3 => "Amber",
        _ => "Red"
    };

    private static string RagInverseCount(int count) => count switch
    {
        0 => "Green",
        <= 2 => "Amber",
        _ => "Red"
    };

    private static string Rag(IEnumerable<string> statuses)
    {
        var list = statuses.ToList();
        if (list.Count == 0) return "NotConfigured";
        if (list.Contains("Red")) return "Red";
        return list.Contains("Amber") ? "Amber" : "Green";
    }

    private static LearnerDeliveryTargetDto ToDto(Guid projectId, LearnerDeliveryTarget? target) => target is null
        ? new LearnerDeliveryTargetDto(projectId, 0, 0, 0, 0, false)
        : new LearnerDeliveryTargetDto(projectId, target.ContractedLearners, target.LearnersDueForCompletion, target.MonitoringVisitsPlanned,
            target.WithdrawalTolerancePercent, true);
}
