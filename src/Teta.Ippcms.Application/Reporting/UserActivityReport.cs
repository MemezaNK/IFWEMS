using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Execution;
using Teta.Ippcms.Domain.Monitoring;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.Application.Reporting;

/// <summary>Per-user activity stats: what they've completed and what they still need to do (FR-REP-002).</summary>
public sealed record UserActivitySummaryDto(
    Guid UserId, string Username, string DisplayName, bool IsActive, IReadOnlyList<string> Roles,
    int DecisionsMade, int CorrectiveActionsCompleted, int RiskTreatmentsCompleted, int IssuesResolved, int AuditActions,
    int TasksCompletedTotal,
    int PendingDecisions, int OpenCorrectiveActions, int OpenRiskTreatments, int OpenIssues,
    int TasksPendingTotal,
    DateTime? LastActivityAtUtc);

public sealed record UserActivityItemDto(string Type, string Reference, string? Detail, string Status, DateTime? OccurredAtUtc);

public sealed record UserActivityDetailDto(UserActivitySummaryDto Summary, IReadOnlyList<UserActivityItemDto> Completed, IReadOnlyList<UserActivityItemDto> Pending);

public sealed record UserActivityReportDto(DateTime GeneratedAtUtc, DateTime? FromUtc, DateTime? ToUtc, IReadOnlyList<UserActivitySummaryDto> Users, UserActivityDetailDto? Detail);

public interface IUserActivityReportService
{
    Task<UserActivityReportDto> GetAsync(Guid? userId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);

    Task<ExportedFile> ExportAsync(Guid? userId, DateTime? fromUtc, DateTime? toUtc, string format, CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the User Activity Report: "Done" = workflow decisions made, completed corrective actions/risk
/// treatments and resolved issues owned by the user. "To do" = pending workflow approvals (tasks whose
/// assigned/escalated role is one of the user's active roles, excluding tasks the user themselves
/// started - segregation of duties), plus open corrective actions/risk treatments/issues owned by the
/// user. Also exports a client-ready PDF with vector bar/pie charts and tables.
/// </summary>
public sealed class UserActivityReportService : IUserActivityReportService
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public UserActivityReportService(ITetaDbContext db, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task<UserActivityReportDto> GetAsync(Guid? userId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var generatedAtUtc = _clock.UtcNow;
        var from = fromUtc ?? generatedAtUtc.AddMonths(-3);
        var to = toUtc ?? generatedAtUtc;
        var today = _clock.Today;

        var usersQuery = _db.Users.AsNoTracking().AsQueryable();
        usersQuery = userId is { } id ? usersQuery.Where(u => u.Id == id) : usersQuery.Where(u => u.IsActive);
        var users = await usersQuery.OrderBy(u => u.DisplayName).ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            return new UserActivityReportDto(generatedAtUtc, fromUtc, toUtc, Array.Empty<UserActivitySummaryDto>(), null);
        }

        var userIds = users.Select(u => u.Id).ToList();

        var roleAssignments = await _db.UserRoleAssignments.AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.Status == AssignmentStatus.Active
                        && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .Select(a => new { a.UserId, a.RoleCode })
            .ToListAsync(cancellationToken);
        var rolesByUser = roleAssignments.GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.RoleCode).Distinct().OrderBy(r => r).ToList());

        var decidedTasks = await _db.WorkflowTasks.AsNoTracking()
            .Where(t => t.DecidedByUserId != null && userIds.Contains(t.DecidedByUserId.Value)
                        && t.Decision != TaskDecision.Pending && t.Decision != TaskDecision.Cancelled
                        && t.DecidedAtUtc != null && t.DecidedAtUtc >= from && t.DecidedAtUtc <= to)
            .ToListAsync(cancellationToken);
        var decidedInstanceIds = decidedTasks.Select(t => t.InstanceId).Distinct().ToList();
        var decidedInstances = await _db.WorkflowInstances.AsNoTracking()
            .Where(i => decidedInstanceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var completedCorrective = await _db.CorrectiveActions.AsNoTracking()
            .Where(a => a.OwnerUserId != null && userIds.Contains(a.OwnerUserId.Value) && a.Status == ActionStatus.Completed
                        && a.CompletedAtUtc != null && a.CompletedAtUtc >= from && a.CompletedAtUtc <= to)
            .ToListAsync(cancellationToken);

        var completedTreatments = await _db.RiskTreatments.AsNoTracking()
            .Where(t => t.OwnerUserId != null && userIds.Contains(t.OwnerUserId.Value) && t.Status == ActionStatus.Completed
                        && t.CompletedAtUtc != null && t.CompletedAtUtc >= from && t.CompletedAtUtc <= to)
            .ToListAsync(cancellationToken);

        var resolvedIssues = await _db.Issues.AsNoTracking()
            .Where(i => i.OwnerUserId != null && userIds.Contains(i.OwnerUserId.Value)
                        && (i.Status == IssueStatus.Resolved || i.Status == IssueStatus.Closed)
                        && i.UpdatedAtUtc != null && i.UpdatedAtUtc >= from && i.UpdatedAtUtc <= to)
            .ToListAsync(cancellationToken);

        var pendingTasks = await (from t in _db.WorkflowTasks.AsNoTracking()
                                   join i in _db.WorkflowInstances.AsNoTracking() on t.InstanceId equals i.Id
                                   where t.Decision == TaskDecision.Pending && i.State == WorkflowState.InProgress
                                   select new { Task = t, Instance = i })
            .ToListAsync(cancellationToken);

        var openCorrective = await _db.CorrectiveActions.AsNoTracking()
            .Where(a => a.OwnerUserId != null && userIds.Contains(a.OwnerUserId.Value) && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress))
            .ToListAsync(cancellationToken);

        var openTreatments = await _db.RiskTreatments.AsNoTracking()
            .Where(t => t.OwnerUserId != null && userIds.Contains(t.OwnerUserId.Value) && (t.Status == ActionStatus.Open || t.Status == ActionStatus.InProgress))
            .ToListAsync(cancellationToken);

        var openIssues = await _db.Issues.AsNoTracking()
            .Where(i => i.OwnerUserId != null && userIds.Contains(i.OwnerUserId.Value)
                        && (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress || i.Status == IssueStatus.Escalated))
            .ToListAsync(cancellationToken);

        var auditCounts = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.UserId != null && userIds.Contains(a.UserId.Value) && a.OccurredAtUtc >= from && a.OccurredAtUtc <= to)
            .GroupBy(a => a.UserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var lastActivity = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.UserId != null && userIds.Contains(a.UserId.Value))
            .GroupBy(a => a.UserId!.Value)
            .Select(g => new { UserId = g.Key, Last = g.Max(a => a.OccurredAtUtc) })
            .ToListAsync(cancellationToken);

        bool TaskAppliesToUser(Guid forUserId, WorkflowTask task, WorkflowInstance instance)
        {
            if (instance.StartedByUserId == forUserId) return false; // segregation of duties
            var roles = rolesByUser.TryGetValue(forUserId, out var r) ? r : new List<string>();
            return roles.Contains(task.AssignedRole) || (task.EscalatedToRole != null && roles.Contains(task.EscalatedToRole));
        }

        var summaries = users.Select(u =>
        {
            var decisions = decidedTasks.Count(t => t.DecidedByUserId == u.Id);
            var corrDone = completedCorrective.Count(a => a.OwnerUserId == u.Id);
            var treatDone = completedTreatments.Count(t => t.OwnerUserId == u.Id);
            var issuesDone = resolvedIssues.Count(i => i.OwnerUserId == u.Id);
            var auditCount = auditCounts.FirstOrDefault(a => a.UserId == u.Id)?.Count ?? 0;
            var pendingDecisions = pendingTasks.Count(r => TaskAppliesToUser(u.Id, r.Task, r.Instance));
            var openCorr = openCorrective.Count(a => a.OwnerUserId == u.Id);
            var openTreat = openTreatments.Count(t => t.OwnerUserId == u.Id);
            var openIss = openIssues.Count(i => i.OwnerUserId == u.Id);
            var roles = rolesByUser.TryGetValue(u.Id, out var rr) ? rr : new List<string>();
            var last = lastActivity.FirstOrDefault(a => a.UserId == u.Id)?.Last;

            return new UserActivitySummaryDto(
                u.Id, u.Username, u.DisplayName, u.IsActive, roles,
                decisions, corrDone, treatDone, issuesDone, auditCount,
                decisions + corrDone + treatDone + issuesDone,
                pendingDecisions, openCorr, openTreat, openIss,
                pendingDecisions + openCorr + openTreat + openIss,
                last);
        }).OrderByDescending(s => s.TasksCompletedTotal).ToList();

        UserActivityDetailDto? detail = null;
        if (userId is { } singleId)
        {
            var user = users.Single(u => u.Id == singleId);
            var completedItems = new List<UserActivityItemDto>();
            completedItems.AddRange(decidedTasks.Where(t => t.DecidedByUserId == user.Id).OrderByDescending(t => t.DecidedAtUtc)
                .Select(t =>
                {
                    decidedInstances.TryGetValue(t.InstanceId, out var instance);
                    var reference = instance?.EntityReference ?? instance?.DefinitionCode ?? t.InstanceId.ToString();
                    return new UserActivityItemDto("Workflow decision", reference, t.StepName, t.Decision.ToString(), t.DecidedAtUtc);
                }));
            completedItems.AddRange(completedCorrective.Where(a => a.OwnerUserId == user.Id).OrderByDescending(a => a.CompletedAtUtc)
                .Select(a => new UserActivityItemDto("Corrective action", a.Number, a.Description, "Completed", a.CompletedAtUtc)));
            completedItems.AddRange(completedTreatments.Where(t => t.OwnerUserId == user.Id).OrderByDescending(t => t.CompletedAtUtc)
                .Select(t => new UserActivityItemDto("Risk treatment", "RT-" + t.Id.ToString("N")[..6].ToUpperInvariant(), t.Description, "Completed", t.CompletedAtUtc)));
            completedItems.AddRange(resolvedIssues.Where(i => i.OwnerUserId == user.Id).OrderByDescending(i => i.UpdatedAtUtc)
                .Select(i => new UserActivityItemDto("Issue", i.Number, i.Title, i.Status.ToString(), i.UpdatedAtUtc)));

            var pendingItems = new List<UserActivityItemDto>();
            pendingItems.AddRange(pendingTasks.Where(r => TaskAppliesToUser(user.Id, r.Task, r.Instance))
                .Select(r =>
                {
                    var reference = r.Instance.EntityReference ?? r.Instance.DefinitionCode;
                    return new UserActivityItemDto("Workflow approval", reference, r.Task.StepName, "Pending", r.Task.DueAtUtc);
                }));
            pendingItems.AddRange(openCorrective.Where(a => a.OwnerUserId == user.Id)
                .Select(a => new UserActivityItemDto("Corrective action", a.Number, a.Description, a.Status.ToString(), a.DueDate.ToDateTime(TimeOnly.MinValue))));
            pendingItems.AddRange(openTreatments.Where(t => t.OwnerUserId == user.Id)
                .Select(t => new UserActivityItemDto("Risk treatment", "RT-" + t.Id.ToString("N")[..6].ToUpperInvariant(), t.Description, t.Status.ToString(), t.DueDate.ToDateTime(TimeOnly.MinValue))));
            pendingItems.AddRange(openIssues.Where(i => i.OwnerUserId == user.Id)
                .Select(i => new UserActivityItemDto("Issue", i.Number, i.Title, i.Status.ToString(), i.DueDate?.ToDateTime(TimeOnly.MinValue))));

            var summary = summaries.Single(s => s.UserId == user.Id);
            detail = new UserActivityDetailDto(summary, completedItems.OrderByDescending(i => i.OccurredAtUtc).ToList(), pendingItems);
            summaries = new List<UserActivitySummaryDto> { summary };
        }

        return new UserActivityReportDto(generatedAtUtc, fromUtc, toUtc, summaries, detail);
    }

    public async Task<ExportedFile> ExportAsync(Guid? userId, DateTime? fromUtc, DateTime? toUtc, string format, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["format"] = new[] { "User Activity Report can only be exported as 'pdf'." }
            });
        }

        var report = await GetAsync(userId, fromUtc, toUtc, cancellationToken);
        var pdf = UserActivityReportPdf.Build(report);
        var stamp = report.GeneratedAtUtc.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var fileName = report.Detail is { } d ? $"User-Activity-{d.Summary.Username}-{stamp}.pdf" : $"User-Activity-Summary-{stamp}.pdf";

        _audit.Write("Reporting", "UserActivityReport", userId?.ToString() ?? "all", "Export", new { userId, fromUtc, toUtc });
        await _db.SaveChangesAsync(cancellationToken);

        return new ExportedFile(pdf, "application/pdf", fileName);
    }
}
