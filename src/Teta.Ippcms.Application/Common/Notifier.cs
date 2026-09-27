using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Platform.Notifications;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Domain.Admin;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Application.Common;

/// <summary>
/// In-system + e-mail notifications from configurable templates (FR-ADM-004). In-system notifications
/// are written in the caller's transaction; e-mails go through the transactional outbox so a failed
/// mail server never rolls back business work.
/// </summary>
public interface INotifier
{
    Task NotifyUsersAsync(IEnumerable<Guid> userIds, string templateCode, IReadOnlyDictionary<string, string?> values,
        string? link = null, string? sourceEntityType = null, Guid? sourceEntityId = null, CancellationToken cancellationToken = default);

    Task NotifyRoleAsync(string roleCode, string templateCode, IReadOnlyDictionary<string, string?> values,
        string? link = null, string? sourceEntityType = null, Guid? sourceEntityId = null, Guid? projectId = null,
        CancellationToken cancellationToken = default);
}

public sealed record EmailOutboxPayload(string[] To, string Subject, string Body, string? AttachmentName = null, string? AttachmentContentType = null,
    string? AttachmentBase64 = null);

public static class NotificationTemplates
{
    public const string WorkflowTaskAssigned = "WORKFLOW_TASK_ASSIGNED";
    public const string WorkflowCompleted = "WORKFLOW_COMPLETED";
    public const string WorkflowEscalated = "WORKFLOW_ESCALATED";
    public const string ContractExpiry = "CONTRACT_EXPIRY";
    public const string DocumentExpiry = "DOCUMENT_EXPIRY";
    public const string OverdueItemEscalated = "OVERDUE_ESCALATED";
    public const string MilestoneOverdue = "MILESTONE_OVERDUE";
    public const string ActionAssigned = "ACTION_ASSIGNED";
    public const string AwardNotification = "AWARD_NOTIFICATION";
    public const string EvidenceMissing = "APP_EVIDENCE_MISSING";
    public const string ErpReconciliationIssue = "ERP_RECONCILIATION";
    public const string ScheduledReport = "SCHEDULED_REPORT";
    public const string SecurityAlert = "SECURITY_ALERT";

    public static readonly IReadOnlyList<(string Code, string Subject, string Body)> Defaults = new[]
    {
        (WorkflowTaskAssigned, "Approval required: {{Title}}", "<p>An approval task <b>{{Step}}</b> is waiting for you on {{Reference}} – {{Title}}.</p><p>Due: {{DueDate}}</p>"),
        (WorkflowCompleted, "{{Reference}} {{Outcome}}", "<p>The approval of {{Reference}} – {{Title}} was <b>{{Outcome}}</b>.</p><p>{{Comment}}</p>"),
        (WorkflowEscalated, "Escalated approval: {{Title}}", "<p>The approval step <b>{{Step}}</b> for {{Reference}} is overdue and has been escalated to you.</p>"),
        (ContractExpiry, "Contract {{Reference}} expires in {{Days}} days", "<p>Contract {{Reference}} – {{Title}} with {{Supplier}} ends on {{EndDate}} ({{Days}} days).</p>"),
        (DocumentExpiry, "Document expiring: {{Title}}", "<p>{{Title}} linked to {{Reference}} expires on {{ExpiryDate}}.</p>"),
        (OverdueItemEscalated, "Overdue {{ItemType}} escalated: {{Reference}}", "<p>{{ItemType}} {{Reference}} ({{Description}}) was due on {{DueDate}} and is escalated to level {{Level}}.</p>"),
        (MilestoneOverdue, "Milestone overdue: {{Name}}", "<p>Milestone {{Name}} on project {{Project}} was due on {{DueDate}}.</p>"),
        (ActionAssigned, "Action assigned: {{Reference}}", "<p>You have been assigned {{ItemType}} {{Reference}}: {{Description}}. Due {{DueDate}}.</p>"),
        (AwardNotification, "Award recorded: {{Reference}}", "<p>Procurement {{Reference}} was awarded to {{Supplier}} for {{Amount}}. Contract creation can proceed.</p>"),
        (EvidenceMissing, "APP evidence outstanding for {{Indicator}}", "<p>{{Count}} result(s) for indicator {{Indicator}} ({{Period}}) are not yet verified.</p>"),
        (ErpReconciliationIssue, "ERP reconciliation exception", "<p>The {{MessageType}} interface batch did not balance: {{Details}}.</p>"),
        (ScheduledReport, "Scheduled report: {{Report}}", "<p>Your scheduled report {{Report}} is attached below.</p><pre>{{Content}}</pre>"),
        (SecurityAlert, "Security alert: {{Event}}", "<p>{{Event}} for user {{User}} at {{Time}}.</p>")
    };
}

public sealed class Notifier : INotifier
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;

    public Notifier(ITetaDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task NotifyUsersAsync(IEnumerable<Guid> userIds, string templateCode, IReadOnlyDictionary<string, string?> values,
        string? link = null, string? sourceEntityType = null, Guid? sourceEntityId = null, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var template = await _db.NotificationTemplates.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Code == templateCode && t.IsActive, cancellationToken);
        var defaults = NotificationTemplates.Defaults.FirstOrDefault(d => d.Code == templateCode);
        var subjectTemplate = template?.Subject ?? defaults.Subject ?? templateCode;
        var bodyTemplate = template?.Body ?? defaults.Body ?? string.Empty;
        var sendEmail = template?.SendEmail ?? true;

        var title = TemplateRenderer.Render(subjectTemplate, values, htmlEncode: false);
        var body = TemplateRenderer.Render(bodyTemplate, values);
        var plain = System.Text.RegularExpressions.Regex.Replace(body, "<.*?>", " ").Trim();

        var users = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive)
            .Select(u => new { u.Id, u.Email }).ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = title.Length > 300 ? title[..300] : title,
                Message = plain.Length > 2000 ? plain[..2000] : plain,
                Link = link,
                Category = templateCode,
                CreatedAtUtc = _clock.UtcNow,
                EmailStatus = sendEmail ? "Queued" : "NotRequired",
                SourceEntityType = sourceEntityType,
                SourceEntityId = sourceEntityId
            });
        }

        var recipients = users.Select(u => u.Email).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().ToArray();
        if (sendEmail && recipients.Length > 0)
        {
            _db.OutboxMessages.Add(new OutboxMessage
            {
                OccurredAtUtc = _clock.UtcNow,
                Type = "Email",
                PayloadJson = JsonSerializer.Serialize(new EmailOutboxPayload(recipients, title, body))
            });
        }
    }

    public async Task NotifyRoleAsync(string roleCode, string templateCode, IReadOnlyDictionary<string, string?> values,
        string? link = null, string? sourceEntityType = null, Guid? sourceEntityId = null, Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var holders = await _db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.RoleCode == roleCode && a.Status == AssignmentStatus.Active
                        && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .Select(a => new { a.UserId, a.ScopeType, a.ScopeId })
            .ToListAsync(cancellationToken);

        // Project-scoped holders are only notified about their own projects.
        var userIds = holders
            .Where(h => h.ScopeType == ScopeType.Global || projectId is null || h.ScopeId == projectId)
            .Select(h => h.UserId);
        await NotifyUsersAsync(userIds, templateCode, values, link, sourceEntityType, sourceEntityId, cancellationToken);
    }
}
