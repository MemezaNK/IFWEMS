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
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Application.Home;

public sealed record HomeKpi(string Label, decimal Value, string Status, string Link);
public sealed record MyActionDto(string Type, Guid Id, string Reference, string Description, DateOnly? DueDate, bool IsOverdue, string Link);
public sealed record DeadlineDto(string Type, string Reference, string Description, DateOnly DueDate, int DaysRemaining, string Link);
public sealed record AlertDto(Guid Id, string Title, string Message, string? Link, DateTime CreatedAtUtc, bool IsRead);
public sealed record RecentRecordDto(string EntityType, string EntityId, string Action, DateTime OccurredAtUtc, string Link);
public sealed record MyProjectDto(Guid Id, string Reference, string Name, string Status, string Stage, string Health, DateOnly? PlannedEnd);

public sealed record HomeDto(string DisplayName, IReadOnlyList<string> Roles, IReadOnlyList<HomeKpi> Kpis, IReadOnlyList<InboxItemDto> Approvals,
    IReadOnlyList<MyActionDto> Actions, IReadOnlyList<DeadlineDto> Deadlines, IReadOnlyList<AlertDto> Alerts, int UnreadAlerts,
    IReadOnlyList<MyProjectDto> MyProjects, IReadOnlyList<RecentRecordDto> Recent);

public interface IHomeService
{
    Task<HomeDto> GetAsync(CancellationToken ct);
}

/// <summary>
/// Role-specific landing page (SRS §13, NFR-016): assigned approvals, actions, deadlines, alerts,
/// recently accessed records and role-relevant KPIs.
/// </summary>
public sealed class HomeService : IHomeService
{
    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAccessScope _scope;
    private readonly IWorkflowService _workflow;

    public HomeService(ITetaDbContext db, ICurrentUser user, IClock clock, IAccessScope scope, IWorkflowService workflow)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _scope = scope;
        _workflow = workflow;
    }

    public async Task<HomeDto> GetAsync(CancellationToken ct)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Sign in required.");
        var today = _clock.Today;
        var horizon = today.AddDays(14);
        var scope = await _scope.GetAsync(ct);

        var approvals = _user.HasPermission(Permissions.WorkflowDecide) ? await _workflow.GetInboxAsync(ct) : Array.Empty<InboxItemDto>();

        // ----- Actions assigned to me -----
        var actions = new List<MyActionDto>();
        var corrective = await _db.CorrectiveActions.AsNoTracking()
            .Where(a => a.OwnerUserId == userId && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress)).ToListAsync(ct);
        actions.AddRange(corrective.Select(a => new MyActionDto("Corrective action", a.Id, a.Number, a.Description, a.DueDate, a.DueDate < today, "/me/actions")));
        var treatments = await _db.RiskTreatments.AsNoTracking()
            .Where(t => t.OwnerUserId == userId && (t.Status == ActionStatus.Open || t.Status == ActionStatus.InProgress)).ToListAsync(ct);
        actions.AddRange(treatments.Select(t => new MyActionDto("Risk treatment", t.Id, "RT-" + t.Id.ToString("N")[..6].ToUpperInvariant(), t.Description,
            t.DueDate, t.DueDate < today, $"/assurance/risks/{t.RiskId}")));
        var issues = await _db.Issues.AsNoTracking()
            .Where(i => i.OwnerUserId == userId && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed).ToListAsync(ct);
        actions.AddRange(issues.Select(i => new MyActionDto("Issue", i.Id, i.Number, i.Title, i.DueDate, i.DueDate < today, $"/projects/{i.ProjectId}/issues")));
        var risks = await _db.Risks.AsNoTracking().Where(r => r.OwnerUserId == userId && r.Status != RiskStatus.Closed && r.ReviewDate <= horizon).ToListAsync(ct);
        actions.AddRange(risks.Select(r => new MyActionDto("Risk review", r.Id, r.Number, r.Title, r.ReviewDate, r.ReviewDate < today, $"/assurance/risks/{r.Id}")));
        var auditActions = await _db.AuditFindings.AsNoTracking()
            .Where(f => f.ActionOwnerUserId == userId && f.Status != FindingStatus.Closed).ToListAsync(ct);
        actions.AddRange(auditActions.Select(f => new MyActionDto("Audit finding", f.Id, f.Number, f.Title, f.DueDate, f.DueDate < today, "/assurance/audit-findings")));
        var obligations = await _db.ContractObligations.AsNoTracking()
            .Where(o => o.OwnerUserId == userId && o.Status == ObligationStatus.Open).ToListAsync(ct);
        actions.AddRange(obligations.Select(o => new MyActionDto("Contract obligation", o.Id, o.Type.ToString(), o.Description, o.DueDate, o.DueDate < today,
            $"/contracts/{o.ContractId}")));
        var wbs = await _db.WbsElements.AsNoTracking()
            .Where(w => w.OwnerUserId == userId && w.Status != WorkStatus.Completed && w.Status != WorkStatus.Cancelled && w.PlannedEnd != null && w.PlannedEnd <= horizon)
            .ToListAsync(ct);
        actions.AddRange(wbs.Select(w => new MyActionDto(w.Type.ToString(), w.Id, w.Code, w.Name, w.PlannedEnd, w.PlannedEnd < today, $"/projects/{w.ProjectId}/schedule")));

        // ----- Deadlines in my projects (next 14 days) -----
        var myProjectsQuery = _db.Projects.AsNoTracking()
            .Where(p => p.ManagerUserId == userId || p.SponsorUserId == userId)
            .Where(p => p.Status != ProjectStatus.Closed && p.Status != ProjectStatus.Cancelled && p.Status != ProjectStatus.Rejected);
        var myProjects = await myProjectsQuery.OrderBy(p => p.PlannedEnd).Take(20).ToListAsync(ct);
        var myProjectIds = myProjects.Select(p => p.Id).ToList();
        var deadlines = new List<DeadlineDto>();
        var milestones = await _db.WbsElements.AsNoTracking()
            .Where(w => myProjectIds.Contains(w.ProjectId) && w.Type == WbsType.Milestone && w.Status != WorkStatus.Completed
                        && w.PlannedEnd != null && w.PlannedEnd <= horizon).ToListAsync(ct);
        deadlines.AddRange(milestones.Select(m => new DeadlineDto("Milestone", m.Code, m.Name, m.PlannedEnd!.Value, m.PlannedEnd.Value.DayNumber - today.DayNumber,
            $"/projects/{m.ProjectId}/schedule")));
        var deliverables = await _db.Deliverables.AsNoTracking()
            .Where(d => myProjectIds.Contains(d.ProjectId) && d.AcceptanceStatus != AcceptanceStatus.Accepted && d.DueDate <= horizon).ToListAsync(ct);
        deadlines.AddRange(deliverables.Select(d => new DeadlineDto("Deliverable", d.Number, d.Name, d.DueDate, d.DueDate.DayNumber - today.DayNumber,
            d.ContractId is { } cid ? $"/contracts/{cid}" : $"/projects/{d.ProjectId}")));
        var contractEnds = await _db.Contracts.AsNoTracking()
            .Where(c => (c.ContractManagerUserId == userId || myProjectIds.Contains(c.ProjectId)) && c.Status == ContractStatus.Active
                        && c.CurrentEndDate <= today.AddDays(90)).ToListAsync(ct);
        deadlines.AddRange(contractEnds.Select(c => new DeadlineDto("Contract end", c.ContractNumber, c.Title, c.CurrentEndDate,
            c.CurrentEndDate.DayNumber - today.DayNumber, $"/contracts/{c.Id}")));
        var visits = await _db.MonitoringVisits.AsNoTracking()
            .Where(v => myProjectIds.Contains(v.ProjectId) && v.Status == VisitStatus.Scheduled && v.ScheduledDate <= horizon).ToListAsync(ct);
        deadlines.AddRange(visits.Select(v => new DeadlineDto("Monitoring visit", v.Number, v.Location ?? v.Type.ToString(), v.ScheduledDate,
            v.ScheduledDate.DayNumber - today.DayNumber, "/me/visits")));

        // ----- Alerts -----
        var alerts = await _db.Notifications.AsNoTracking().Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAtUtc).Take(8)
            .Select(n => new AlertDto(n.Id, n.Title, n.Message, n.Link, n.CreatedAtUtc, n.IsRead)).ToListAsync(ct);
        var unread = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        // ----- Recently accessed/changed records (from my audit trail) -----
        var since = _clock.UtcNow.AddDays(-30);
        var recentRaw = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.UserId == userId && a.OccurredAtUtc >= since && a.EntityId != null && a.Module != "Security")
            .OrderByDescending(a => a.Id).Take(200)
            .Select(a => new { a.EntityType, a.EntityId, a.Action, a.OccurredAtUtc }).ToListAsync(ct);
        var recent = recentRaw.GroupBy(a => (a.EntityType, a.EntityId)).Select(g => g.First())
            .Where(a => LinkFor(a.EntityType, a.EntityId!) is not null).Take(10)
            .Select(a => new RecentRecordDto(a.EntityType, a.EntityId!, a.Action, a.OccurredAtUtc, LinkFor(a.EntityType, a.EntityId!)!)).ToList();

        // ----- Role KPIs -----
        var kpis = await KpisAsync(scope, userId, actions, approvals, ct);

        return new HomeDto(_user.DisplayName ?? _user.Username ?? string.Empty, _user.Roles.ToList(), kpis, approvals,
            actions.OrderBy(a => a.DueDate ?? DateOnly.MaxValue).ToList(), deadlines.OrderBy(d => d.DueDate).ToList(), alerts, unread,
            myProjects.Select(p => new MyProjectDto(p.Id, p.Reference, p.Name, p.Status.ToString(), p.Stage.ToString(), p.Health.ToString(), p.PlannedEnd)).ToList(),
            recent);
    }

    internal static string? LinkFor(string entityType, string id) => entityType switch
    {
        "Project" => $"/projects/{id}",
        "Procurement" => $"/procurement/{id}",
        "Requisition" => "/procurement/requisitions",
        "Contract" => $"/contracts/{id}",
        "Invoice" => $"/finance/invoices/{id}",
        "Risk" => $"/assurance/risks/{id}",
        "Supplier" => $"/suppliers/{id}",
        "StrategicPlan" => $"/strategy/plans/{id}",
        "ChangeRequest" => "/projects",
        "MonitoringVisit" => "/me/visits",
        "AuditFinding" => "/assurance/audit-findings",
        "BoardReportPack" => "/reports/board-packs",
        _ => null
    };

    private async Task<List<HomeKpi>> KpisAsync(DataScope scope, Guid userId, List<MyActionDto> actions, IReadOnlyList<InboxItemDto> approvals, CancellationToken ct)
    {
        var today = _clock.Today;
        var kpis = new List<HomeKpi>
        {
            new("Approvals waiting", approvals.Count, approvals.Any(a => a.IsOverdue) ? "Red" : approvals.Count > 0 ? "Amber" : "Green", "/inbox"),
            new("My overdue actions", actions.Count(a => a.IsOverdue), actions.Any(a => a.IsOverdue) ? "Red" : "Green", "/")
        };

        if (_user.HasPermission(Permissions.PortfolioRead))
        {
            var projects = await _db.Projects.AsNoTracking().InScope(scope, p => p.Id)
                .Where(p => p.Status == ProjectStatus.InExecution).Select(p => p.Health).ToListAsync(ct);
            kpis.Add(new HomeKpi("Projects in execution", projects.Count, "Info", "/projects?status=InExecution"));
            var red = projects.Count(h => h == HealthStatus.Red);
            kpis.Add(new HomeKpi("Projects in red", red, red > 0 ? "Red" : "Green", "/projects?health=Red"));
        }
        if (_user.HasPermission(Permissions.ProcurementManage))
        {
            var open = await _db.Procurements.AsNoTracking().InScope(scope, p => p.ProjectId)
                .CountAsync(p => p.Status != ProcurementStatus.Awarded && p.Status != ProcurementStatus.Cancelled, ct);
            kpis.Add(new HomeKpi("Procurements in progress", open, "Info", "/procurement"));
        }
        if (_user.HasPermission(Permissions.ContractManage))
        {
            var limit = today.AddDays(90);
            var expiring = await _db.Contracts.AsNoTracking().InScope(scope, c => c.ProjectId)
                .CountAsync(c => c.Status == ContractStatus.Active && c.CurrentEndDate <= limit, ct);
            kpis.Add(new HomeKpi("Contracts expiring (90 days)", expiring, expiring > 0 ? "Amber" : "Green", "/contracts?expiring=true"));
        }
        if (_user.HasPermission(Permissions.FinanceManage))
        {
            var pending = await _db.Invoices.AsNoTracking().InScope(scope, i => i.ProjectId)
                .CountAsync(i => i.Status == InvoiceStatus.Registered || i.Status == InvoiceStatus.PendingCertification || i.Status == InvoiceStatus.ValidationFailed, ct);
            kpis.Add(new HomeKpi("Invoices to process", pending, pending > 0 ? "Amber" : "Green", "/finance/invoices"));
        }
        if (_user.HasPermission(Permissions.MeManage))
        {
            var overdueVisits = await _db.MonitoringVisits.AsNoTracking().InScope(scope, v => v.ProjectId)
                .CountAsync(v => v.Status == VisitStatus.Scheduled && v.ScheduledDate < today, ct);
            kpis.Add(new HomeKpi("Overdue monitoring visits", overdueVisits, overdueVisits > 0 ? "Red" : "Green", "/me/visits"));
        }
        if (_user.HasPermission(Permissions.RiskManage) || _user.HasPermission(Permissions.AssuranceManage))
        {
            var critical = await _db.Risks.AsNoTracking().InScopeNullable(scope, r => r.ProjectId)
                .CountAsync(r => r.Status != RiskStatus.Closed && r.ResidualRating == "Critical", ct);
            kpis.Add(new HomeKpi("Critical risks", critical, critical > 0 ? "Red" : "Green", "/assurance/risks?rating=Critical"));
        }
        if (_user.HasPermission(Permissions.PerformanceVerify))
        {
            var toVerify = await _db.PerformanceResults.AsNoTracking().CountAsync(r => r.Status == Domain.Strategy.ResultStatus.Submitted, ct);
            kpis.Add(new HomeKpi("Results awaiting verification", toVerify, toVerify > 0 ? "Amber" : "Green", "/strategy/results"));
        }
        if (_user.HasPermission(Permissions.DataQualityManage))
        {
            var dq = await _db.DataQualityIssues.AsNoTracking().CountAsync(i => i.Status == Domain.Reporting.DataQualityStatus.Open, ct);
            kpis.Add(new HomeKpi("Open data-quality issues", dq, dq > 0 ? "Amber" : "Green", "/reports/data-quality"));
        }
        if (_user.HasPermission(Permissions.SecurityRolesApprove))
        {
            var pendingRoles = await _db.UserRoleAssignments.AsNoTracking().CountAsync(a => a.Status == AssignmentStatus.PendingApproval && a.RequestedByUserId != userId, ct);
            kpis.Add(new HomeKpi("Role requests to approve", pendingRoles, pendingRoles > 0 ? "Amber" : "Green", "/admin/users"));
        }
        return kpis;
    }
}
