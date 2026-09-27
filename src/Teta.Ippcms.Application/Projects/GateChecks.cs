using Microsoft.EntityFrameworkCore;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Execution;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Monitoring;
using Teta.Ippcms.Domain.Projects;

namespace Teta.Ippcms.Application.Projects;

/// <summary>
/// Automatic stage-gate and close-out checks (FR-PPM-009, FR-EXE-014, BR-010). Each check returns
/// whether it is met and a short evidence statement so reviewers can see why.
/// </summary>
public interface IGateChecks
{
    Task<(bool Automatic, bool Met, string? Evidence)> EvaluateAsync(Guid projectId, string checkCode, CancellationToken ct);

    /// <summary>Open mandatory items that block project closure (BR-010).</summary>
    Task<IReadOnlyList<string>> OpenClosureItemsAsync(Guid projectId, CancellationToken ct);
}

public static class GateCheckCodes
{
    public const string BusinessCaseApproved = "BUSINESS_CASE_APPROVED";
    public const string IndicatorAligned = "INDICATOR_ALIGNED";
    public const string CharterApproved = "CHARTER_APPROVED";
    public const string StakeholdersDefined = "STAKEHOLDERS_DEFINED";
    public const string BudgetReconciled = "BUDGET_RECONCILED";
    public const string BaselineApproved = "BASELINE_APPROVED";
    public const string RisksRegistered = "RISKS_REGISTERED";
    public const string MePlanDefined = "ME_PLAN_DEFINED";
    public const string NoOpenCriticalIssues = "NO_OPEN_CRITICAL_ISSUES";
    public const string MilestonesComplete = "MILESTONES_COMPLETE";
    public const string DeliverablesAccepted = "DELIVERABLES_ACCEPTED";
    public const string ContractsClosed = "CONTRACTS_CLOSED";
    public const string InvoicesSettled = "INVOICES_SETTLED";
    public const string ClosureApproved = "CLOSURE_APPROVED";
    public const string BenefitReviewScheduled = "BENEFIT_REVIEW_SCHEDULED";
    public const string BenefitReviewCompleted = "BENEFIT_REVIEW_COMPLETED";

    public static readonly IReadOnlyList<string> All = new[]
    {
        BusinessCaseApproved, IndicatorAligned, CharterApproved, StakeholdersDefined, BudgetReconciled, BaselineApproved, RisksRegistered,
        MePlanDefined, NoOpenCriticalIssues, MilestonesComplete, DeliverablesAccepted, ContractsClosed, InvoicesSettled, ClosureApproved,
        BenefitReviewScheduled, BenefitReviewCompleted
    };
}

public sealed class GateChecks : IGateChecks
{
    private readonly ITetaDbContext _db;

    public GateChecks(ITetaDbContext db) => _db = db;

    public async Task<(bool Automatic, bool Met, string? Evidence)> EvaluateAsync(Guid projectId, string checkCode, CancellationToken ct)
    {
        switch (checkCode)
        {
            case GateCheckCodes.BusinessCaseApproved:
                var bcApproved = await _db.BusinessCases.AnyAsync(b => b.ProjectId == projectId && b.Status == BusinessCaseStatus.Approved, ct);
                return (true, bcApproved, bcApproved ? "Business case approved" : "No approved business case");

            case GateCheckCodes.IndicatorAligned:
                var links = await _db.ProjectIndicatorLinks.CountAsync(l => l.ProjectId == projectId, ct);
                return (true, links > 0, $"{links} APP indicator link(s)");

            case GateCheckCodes.CharterApproved:
                var charter = await _db.ProjectCharters.AnyAsync(c => c.ProjectId == projectId && c.Status == CharterStatus.Approved, ct);
                return (true, charter, charter ? "Charter approved" : "Charter not approved");

            case GateCheckCodes.StakeholdersDefined:
                var roles = await _db.ProjectStakeholders.Where(s => s.ProjectId == projectId).Select(s => s.Role).Distinct().ToListAsync(ct);
                var ok = roles.Contains(StakeholderRole.Sponsor) && roles.Contains(StakeholderRole.ProjectManager);
                return (true, ok, ok ? "Sponsor and project manager assigned" : "Sponsor and/or project manager not recorded as stakeholders");

            case GateCheckCodes.BudgetReconciled:
                var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
                var revised = (await _db.BudgetLines.Where(b => b.ProjectId == projectId).Select(b => b.RevisedAmount).ToListAsync(ct)).Sum();
                var reconciled = revised > 0 && Math.Abs(revised - project.ApprovedBudget) < 0.01m;
                return (true, reconciled, $"Budget lines R {revised:N2} vs approved R {project.ApprovedBudget:N2}");

            case GateCheckCodes.BaselineApproved:
                var baseline = await _db.ScheduleBaselines.AnyAsync(b => b.ProjectId == projectId, ct);
                return (true, baseline, baseline ? "Schedule baseline approved" : "No approved schedule baseline");

            case GateCheckCodes.RisksRegistered:
                var risks = await _db.Risks.CountAsync(r => r.ProjectId == projectId, ct);
                return (true, risks > 0, $"{risks} risk(s) registered");

            case GateCheckCodes.MePlanDefined:
                var me = await _db.MePlans.AnyAsync(m => m.ProjectId == projectId, ct);
                return (true, me, me ? "M&E plan defined" : "No M&E plan");

            case GateCheckCodes.NoOpenCriticalIssues:
                var critical = await _db.Issues.CountAsync(i => i.ProjectId == projectId && i.Severity == Severity.Critical
                    && (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress || i.Status == IssueStatus.Escalated), ct);
                return (true, critical == 0, $"{critical} open critical issue(s)");

            case GateCheckCodes.MilestonesComplete:
                var openMilestones = await _db.WbsElements.CountAsync(w => w.ProjectId == projectId && w.Type == WbsType.Milestone
                    && w.Status != WorkStatus.Completed && w.Status != WorkStatus.Cancelled, ct);
                return (true, openMilestones == 0, $"{openMilestones} milestone(s) not complete");

            case GateCheckCodes.DeliverablesAccepted:
                var pendingDeliverables = await _db.Deliverables.CountAsync(d => d.ProjectId == projectId && d.AcceptanceStatus != AcceptanceStatus.Accepted, ct);
                return (true, pendingDeliverables == 0, $"{pendingDeliverables} deliverable(s) not accepted");

            case GateCheckCodes.ContractsClosed:
                var openContracts = await _db.Contracts.CountAsync(c => c.ProjectId == projectId
                    && c.Status != ContractStatus.Closed && c.Status != ContractStatus.Terminated, ct);
                return (true, openContracts == 0, $"{openContracts} contract(s) not closed");

            case GateCheckCodes.InvoicesSettled:
                var unsettled = await _db.Invoices.CountAsync(i => i.ProjectId == projectId && i.Status != InvoiceStatus.Paid
                    && i.Status != InvoiceStatus.Rejected && i.Status != InvoiceStatus.Cancelled, ct);
                return (true, unsettled == 0, $"{unsettled} invoice(s) not settled");

            case GateCheckCodes.ClosureApproved:
                var closure = await _db.ProjectClosures.AnyAsync(c => c.ProjectId == projectId && c.Status == ClosureStatus.Approved, ct);
                return (true, closure, closure ? "Closure approved" : "Closure not approved");

            case GateCheckCodes.BenefitReviewScheduled:
                var scheduled = await _db.BenefitReviews.AnyAsync(b => b.ProjectId == projectId, ct);
                return (true, scheduled, scheduled ? "Benefit review scheduled" : "No benefit review scheduled");

            case GateCheckCodes.BenefitReviewCompleted:
                var completed = await _db.BenefitReviews.AnyAsync(b => b.ProjectId == projectId && b.Status == BenefitReviewStatus.Completed, ct);
                return (true, completed, completed ? "Benefit review completed" : "Benefit review not completed");

            default:
                return (false, false, null);
        }
    }

    public async Task<IReadOnlyList<string>> OpenClosureItemsAsync(Guid projectId, CancellationToken ct)
    {
        var items = new List<string>();
        foreach (var code in new[]
                 {
                     GateCheckCodes.MilestonesComplete, GateCheckCodes.DeliverablesAccepted, GateCheckCodes.ContractsClosed,
                     GateCheckCodes.InvoicesSettled, GateCheckCodes.NoOpenCriticalIssues
                 })
        {
            var (_, met, evidence) = await EvaluateAsync(projectId, code, ct);
            if (!met) items.Add(evidence ?? code);
        }

        var openActions = await _db.CorrectiveActions.CountAsync(a => a.ProjectId == projectId
            && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress), ct);
        if (openActions > 0) items.Add($"{openActions} open corrective action(s)");

        var openFindings = await _db.AuditFindings.CountAsync(f => f.ProjectId == projectId && f.Status != FindingStatus.Closed, ct);
        if (openFindings > 0) items.Add($"{openFindings} open audit finding(s)");

        var unverified = await _db.PerformanceResults.CountAsync(r => r.ProjectId == projectId
            && r.Status != Domain.Strategy.ResultStatus.Verified && r.Status != Domain.Strategy.ResultStatus.Rejected, ct);
        if (unverified > 0) items.Add($"{unverified} unverified performance result(s)");

        return items;
    }
}
