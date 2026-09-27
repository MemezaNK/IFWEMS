using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Core;
using Platform.Notifications;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Execution;
using Teta.Ippcms.Domain.Monitoring;
using Teta.Ippcms.Domain.Reporting;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Strategy;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.Application.Jobs;

/// <summary>
/// Scheduled/background work (SRS §8.3 batch jobs): e-mail outbox dispatch, overdue escalations
/// (FR-ADM-005), expiry alerts (FR-CON-006), milestone alerts, APP evidence completeness,
/// retention review and session housekeeping. Each method is idempotent and safe to re-run.
/// </summary>
public interface IScheduledJobs
{
    Task<int> DispatchOutboxAsync(CancellationToken ct);
    Task<int> EscalateOverdueItemsAsync(CancellationToken ct);
    Task<int> ContractExpiryAlertsAsync(CancellationToken ct);
    Task<int> DocumentExpiryAlertsAsync(CancellationToken ct);
    Task<int> MilestoneOverdueAlertsAsync(CancellationToken ct);
    Task<int> AppEvidenceCompletenessAsync(CancellationToken ct);
    Task<int> RetentionReviewAsync(CancellationToken ct);
    Task<int> SessionHousekeepingAsync(CancellationToken ct);
}

public sealed class ScheduledJobs : IScheduledJobs
{
    private const int MaxEmailAttempts = 5;

    private readonly ITetaDbContext _db;
    private readonly IClock _clock;
    private readonly ISettings _settings;
    private readonly INotifier _notifier;
    private readonly IEmailSender _email;
    private readonly ILogger<ScheduledJobs> _logger;

    public ScheduledJobs(ITetaDbContext db, IClock clock, ISettings settings, INotifier notifier, IEmailSender email, ILogger<ScheduledJobs> logger)
    {
        _db = db;
        _clock = clock;
        _settings = settings;
        _notifier = notifier;
        _email = email;
        _logger = logger;
    }

    // ----- Outbox -----
    public async Task<int> DispatchOutboxAsync(CancellationToken ct)
    {
        var pending = await _db.OutboxMessages.Where(m => m.ProcessedAtUtc == null && m.Attempts < MaxEmailAttempts)
            .OrderBy(m => m.OccurredAtUtc).Take(50).ToListAsync(ct);
        var sent = 0;
        foreach (var message in pending)
        {
            message.Attempts++;
            if (message.Type != "Email")
            {
                message.ProcessedAtUtc = _clock.UtcNow;
                message.LastError = $"Unknown outbox message type '{message.Type}'.";
                continue;
            }
            try
            {
                var payload = JsonSerializer.Deserialize<EmailOutboxPayload>(message.PayloadJson)
                              ?? throw new JsonException("Empty payload");
                var attachments = payload.AttachmentBase64 is { Length: > 0 } b64 && payload.AttachmentName is not null
                    ? new[] { new EmailAttachment(payload.AttachmentName, payload.AttachmentContentType ?? "application/octet-stream", Convert.FromBase64String(b64)) }
                    : null;
                var result = await _email.SendAsync(new EmailMessage(payload.To, payload.Subject, payload.Body, attachments), ct);
                if (result.Sent || result == EmailSendResult.Disabled)
                {
                    message.ProcessedAtUtc = _clock.UtcNow;
                    message.LastError = result.Sent ? null : result.Error;
                    if (result.Sent) sent++;
                }
                else
                {
                    message.LastError = result.Error;
                }
            }
            catch (Exception ex) when (ex is JsonException or FormatException)
            {
                message.ProcessedAtUtc = _clock.UtcNow;
                message.LastError = "Invalid payload: " + ex.Message;
            }
        }
        if (pending.Count > 0) await _db.SaveChangesAsync(ct);
        return sent;
    }

    // ----- Overdue escalation (FR-ADM-005) -----
    public async Task<int> EscalateOverdueItemsAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var grace = await _settings.GetIntAsync(SettingKeys.EscalationGraceDays, ct);
        var cutoff = _clock.Today.AddDays(-grace);
        var level1 = await _settings.GetAsync(SettingKeys.EscalationRoleLevel1, ct);
        var level2 = await _settings.GetAsync(SettingKeys.EscalationRoleLevel2, ct);
        var reEscalateBefore = now.AddDays(-7);
        var count = 0;

        async Task EscalateAsync(string itemType, Guid id, string reference, string description, DateOnly due, Guid? projectId, int newLevel, Guid? ownerUserId)
        {
            var role = newLevel == 1 ? level1 : level2;
            _db.EscalationEvents.Add(new EscalationEvent
            {
                EntityType = itemType, EntityId = id, EntityReference = reference, Level = newLevel, EscalatedToRole = role,
                Reason = $"{itemType} overdue since {due:yyyy-MM-dd}", OccurredAtUtc = now
            });
            var values = new Dictionary<string, string?>
            {
                ["ItemType"] = itemType, ["Reference"] = reference, ["Description"] = description.Length > 200 ? description[..200] : description,
                ["DueDate"] = due.ToString("yyyy-MM-dd"), ["Level"] = newLevel.ToString()
            };
            await _notifier.NotifyRoleAsync(role, NotificationTemplates.OverdueItemEscalated, values, null, itemType, id, projectId, ct);
            if (ownerUserId is { } owner)
                await _notifier.NotifyUsersAsync(new[] { owner }, NotificationTemplates.OverdueItemEscalated, values, null, itemType, id, ct);
            count++;
        }

        var actions = await _db.CorrectiveActions.Where(a => (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress) && a.DueDate < cutoff
                                                             && a.EscalationLevel < 2 && (a.LastEscalatedAtUtc == null || a.LastEscalatedAtUtc < reEscalateBefore))
            .ToListAsync(ct);
        foreach (var a in actions)
        {
            a.EscalationLevel++;
            a.LastEscalatedAtUtc = now;
            await EscalateAsync("Corrective action", a.Id, a.Number, a.Description, a.DueDate, a.ProjectId, a.EscalationLevel, a.OwnerUserId);
        }

        var treatments = await _db.RiskTreatments.Where(t => (t.Status == ActionStatus.Open || t.Status == ActionStatus.InProgress) && t.DueDate < cutoff
                                                             && t.EscalationLevel < 2 && (t.LastEscalatedAtUtc == null || t.LastEscalatedAtUtc < reEscalateBefore))
            .ToListAsync(ct);
        foreach (var t in treatments)
        {
            t.EscalationLevel++;
            t.LastEscalatedAtUtc = now;
            await EscalateAsync("Risk treatment", t.Id, "RT-" + t.Id.ToString("N")[..6].ToUpperInvariant(), t.Description, t.DueDate, t.ProjectId,
                t.EscalationLevel, t.OwnerUserId);
        }

        var issues = await _db.Issues.Where(i => i.DueDate != null && i.DueDate < cutoff && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed
                                                 && i.EscalationLevel < 2 && (i.LastEscalatedAtUtc == null || i.LastEscalatedAtUtc < reEscalateBefore))
            .ToListAsync(ct);
        foreach (var i in issues)
        {
            i.EscalationLevel++;
            i.LastEscalatedAtUtc = now;
            i.Status = IssueStatus.Escalated;
            await EscalateAsync("Issue", i.Id, i.Number, i.Title, i.DueDate!.Value, i.ProjectId, i.EscalationLevel, i.OwnerUserId);
        }

        // Audit findings have no escalation counter: escalate once per week while overdue.
        var findings = await _db.AuditFindings.Where(f => f.Status != FindingStatus.Closed && f.DueDate < cutoff).ToListAsync(ct);
        var findingIds = findings.Select(f => f.Id).ToList();
        var recent = await _db.EscalationEvents.Where(e => findingIds.Contains(e.EntityId) && e.OccurredAtUtc >= reEscalateBefore)
            .Select(e => e.EntityId).ToListAsync(ct);
        var previousLevels = await _db.EscalationEvents.Where(e => findingIds.Contains(e.EntityId)).GroupBy(e => e.EntityId)
            .Select(g => new { g.Key, Level = g.Max(e => e.Level) }).ToListAsync(ct);
        foreach (var f in findings.Where(f => !recent.Contains(f.Id)))
        {
            var level = Math.Min(2, (previousLevels.FirstOrDefault(p => p.Key == f.Id)?.Level ?? 0) + 1);
            await EscalateAsync("Audit finding", f.Id, f.Number, f.Title, f.DueDate, f.ProjectId, level, f.ActionOwnerUserId);
        }

        if (count > 0) await _db.SaveChangesAsync(ct);
        return count;
    }

    // ----- Expiry alerts (FR-CON-006) -----
    public async Task<int> ContractExpiryAlertsAsync(CancellationToken ct)
    {
        var today = _clock.Today;
        var leads = (await _settings.GetIntListAsync(SettingKeys.ContractExpiryLeadDays, ct)).Where(l => l > 0).OrderBy(l => l).ToList();
        if (leads.Count == 0) return 0;
        var horizon = today.AddDays(leads.Max());
        var contracts = await _db.Contracts.AsNoTracking()
            .Where(c => c.Status == ContractStatus.Active && c.CurrentEndDate >= today && c.CurrentEndDate <= horizon).ToListAsync(ct);
        var ids = contracts.Select(c => c.Id).ToList();
        var sent = await _db.Notifications.AsNoTracking()
            .Where(n => n.Category == NotificationTemplates.ContractExpiry && n.SourceEntityId != null && ids.Contains(n.SourceEntityId.Value))
            .Select(n => new { n.SourceEntityId, n.CreatedAtUtc }).ToListAsync(ct);
        var supplierIds = contracts.Select(c => c.SupplierId).Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.LegalName, ct);
        var count = 0;
        foreach (var c in contracts)
        {
            var days = c.CurrentEndDate.DayNumber - today.DayNumber;
            var lead = leads.First(l => days <= l);
            var windowStart = c.CurrentEndDate.AddDays(-lead).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            if (sent.Any(s => s.SourceEntityId == c.Id && s.CreatedAtUtc >= windowStart)) continue;

            var values = new Dictionary<string, string?>
            {
                ["Reference"] = c.ContractNumber, ["Title"] = c.Title, ["Supplier"] = suppliers.GetValueOrDefault(c.SupplierId),
                ["EndDate"] = c.CurrentEndDate.ToString("yyyy-MM-dd"), ["Days"] = days.ToString()
            };
            var recipients = new List<Guid>();
            if (c.ContractManagerUserId is { } cm) recipients.Add(cm);
            var manager = await _db.Projects.AsNoTracking().Where(p => p.Id == c.ProjectId).Select(p => p.ManagerUserId).SingleOrDefaultAsync(ct);
            if (manager is { } pm) recipients.Add(pm);
            if (recipients.Count > 0)
                await _notifier.NotifyUsersAsync(recipients, NotificationTemplates.ContractExpiry, values, $"/contracts/{c.Id}", "Contract", c.Id, ct);
            else
                await _notifier.NotifyRoleAsync(Roles.HeadScm, NotificationTemplates.ContractExpiry, values, $"/contracts/{c.Id}", "Contract", c.Id, c.ProjectId, ct);
            count++;
        }
        if (count > 0) await _db.SaveChangesAsync(ct);
        return count;
    }

    public async Task<int> DocumentExpiryAlertsAsync(CancellationToken ct)
    {
        var today = _clock.Today;
        var leads = (await _settings.GetIntListAsync(SettingKeys.DocumentExpiryLeadDays, ct)).Where(l => l > 0).OrderBy(l => l).ToList();
        if (leads.Count == 0) return 0;
        var horizon = today.AddDays(leads.Max());
        var documents = await _db.Documents.AsNoTracking()
            .Where(d => d.IsLatest && d.ExpiryDate != null && d.ExpiryDate >= today && d.ExpiryDate <= horizon).ToListAsync(ct);
        var ids = documents.Select(d => d.Id).ToList();
        var sent = await _db.Notifications.AsNoTracking()
            .Where(n => n.Category == NotificationTemplates.DocumentExpiry && n.SourceEntityId != null && ids.Contains(n.SourceEntityId.Value))
            .Select(n => new { n.SourceEntityId, n.CreatedAtUtc }).ToListAsync(ct);
        var count = 0;
        foreach (var d in documents)
        {
            var days = d.ExpiryDate!.Value.DayNumber - today.DayNumber;
            var lead = leads.First(l => days <= l);
            var windowStart = d.ExpiryDate.Value.AddDays(-lead).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            if (sent.Any(s => s.SourceEntityId == d.Id && s.CreatedAtUtc >= windowStart)) continue;

            var recipients = new List<Guid>();
            if (d.CreatedByUserId is { } uploader) recipients.Add(uploader);
            if (d.ParentType == ParentTypes.Contract)
            {
                var cm = await _db.Contracts.AsNoTracking().Where(c => c.Id == d.ParentId).Select(c => c.ContractManagerUserId).SingleOrDefaultAsync(ct);
                if (cm is { } id) recipients.Add(id);
            }
            if (d.ProjectId is { } pid)
            {
                var pm = await _db.Projects.AsNoTracking().Where(p => p.Id == pid).Select(p => p.ManagerUserId).SingleOrDefaultAsync(ct);
                if (pm is { } id) recipients.Add(id);
            }
            if (recipients.Count == 0) continue;
            await _notifier.NotifyUsersAsync(recipients, NotificationTemplates.DocumentExpiry, new Dictionary<string, string?>
            {
                ["Title"] = d.Title, ["Reference"] = $"{d.ParentType} {d.DocumentType}", ["ExpiryDate"] = d.ExpiryDate.Value.ToString("yyyy-MM-dd")
            }, $"/documents/{d.Id}", "Document", d.Id, ct);
            count++;
        }
        if (count > 0) await _db.SaveChangesAsync(ct);
        return count;
    }

    public async Task<int> MilestoneOverdueAlertsAsync(CancellationToken ct)
    {
        var today = _clock.Today;
        var milestones = await _db.WbsElements.AsNoTracking()
            .Where(w => w.Type == WbsType.Milestone && w.Status != WorkStatus.Completed && w.Status != WorkStatus.Cancelled
                        && w.PlannedEnd != null && w.PlannedEnd < today).ToListAsync(ct);
        var ids = milestones.Select(m => m.Id).ToList();
        var already = (await _db.Notifications.AsNoTracking()
            .Where(n => n.Category == NotificationTemplates.MilestoneOverdue && n.SourceEntityId != null && ids.Contains(n.SourceEntityId.Value))
            .Select(n => n.SourceEntityId!.Value).ToListAsync(ct)).ToHashSet();
        var projectIds = milestones.Select(m => m.ProjectId).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var count = 0;
        foreach (var m in milestones.Where(m => !already.Contains(m.Id)))
        {
            projects.TryGetValue(m.ProjectId, out var project);
            var recipients = new[] { m.OwnerUserId, project?.ManagerUserId }.Where(x => x is not null).Select(x => x!.Value).ToList();
            if (recipients.Count == 0) continue;
            await _notifier.NotifyUsersAsync(recipients, NotificationTemplates.MilestoneOverdue, new Dictionary<string, string?>
            {
                ["Name"] = m.Name, ["Project"] = project?.Reference, ["DueDate"] = m.PlannedEnd!.Value.ToString("yyyy-MM-dd")
            }, $"/projects/{m.ProjectId}/schedule", "Milestone", m.Id, ct);
            count++;
        }
        if (count > 0) await _db.SaveChangesAsync(ct);
        return count;
    }

    // ----- APP evidence completeness (FR-STR / RPT-013) -----
    public async Task<int> AppEvidenceCompletenessAsync(CancellationToken ct)
    {
        var fy = await _settings.GetAsync(SettingKeys.CurrentFinancialYear, ct);
        var unverified = await _db.PerformanceResults.AsNoTracking()
            .Where(r => r.FinancialYear == fy && (r.Status == ResultStatus.Captured || r.Status == ResultStatus.Submitted))
            .Select(r => new { r.IndicatorId, r.Quarter }).ToListAsync(ct);
        if (unverified.Count == 0) return 0;
        var indicatorIds = unverified.Select(u => u.IndicatorId).Distinct().ToList();
        var indicators = await _db.AppIndicators.AsNoTracking().Where(i => indicatorIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var count = 0;
        foreach (var group in unverified.GroupBy(u => (u.IndicatorId, u.Quarter)))
        {
            if (!indicators.TryGetValue(group.Key.IndicatorId, out var indicator)) continue;
            var values = new Dictionary<string, string?>
            {
                ["Indicator"] = $"{indicator.Code} {indicator.Name}", ["Count"] = group.Count().ToString(), ["Period"] = $"{fy} Q{group.Key.Quarter}"
            };
            if (indicator.ResponsibleUserId is { } responsible)
                await _notifier.NotifyUsersAsync(new[] { responsible }, NotificationTemplates.EvidenceMissing, values, "/strategy/results", "AppIndicator", indicator.Id, ct);
            else
                await _notifier.NotifyRoleAsync(Roles.StrategyOfficer, NotificationTemplates.EvidenceMissing, values, "/strategy/results", "AppIndicator", indicator.Id, null, ct);
            count++;
        }
        await _db.SaveChangesAsync(ct);
        return count;
    }

    // ----- Retention review (SRS §14) -----
    public async Task<int> RetentionReviewAsync(CancellationToken ct)
    {
        var policies = await _db.RetentionPolicies.AsNoTracking().Where(p => p.IsActive).ToListAsync(ct);
        var count = 0;
        foreach (var policy in policies)
        {
            var threshold = _clock.UtcNow.AddYears(-policy.RetentionYears);
            var due = await _db.Documents.AsNoTracking()
                .Where(d => d.RetentionClass == policy.RecordClass && d.CreatedAtUtc < threshold).Select(d => new { d.Id, d.Title, d.DocumentType })
                .Take(500).ToListAsync(ct);
            var ids = due.Select(d => (Guid?)d.Id).ToList();
            var existing = (await _db.DataQualityIssues.AsNoTracking().Where(i => i.RuleCode == "RET-001" && ids.Contains(i.EntityId))
                .Select(i => i.EntityId).ToListAsync(ct)).ToHashSet();
            foreach (var d in due.Where(d => !existing.Contains(d.Id)))
            {
                _db.DataQualityIssues.Add(new DataQualityIssue
                {
                    RuleCode = "RET-001",
                    Description = $"{d.DocumentType} '{d.Title}' has passed its {policy.RetentionYears}-year retention period ({policy.RecordClass}); action: {policy.DisposalAction}.",
                    EntityType = "Document", EntityId = d.Id, EntityReference = d.Title.Length > 200 ? d.Title[..200] : d.Title,
                    Severity = Severity.Low, OwnerName = "Records management", DetectedAtUtc = _clock.UtcNow
                });
                count++;
            }
        }
        if (count > 0) await _db.SaveChangesAsync(ct);
        return count;
    }

    public async Task<int> SessionHousekeepingAsync(CancellationToken ct)
    {
        var cutoff = _clock.UtcNow.AddDays(-30);
        var old = await _db.UserSessions.Where(s => s.ExpiresAtUtc < cutoff).Take(1000).ToListAsync(ct);
        foreach (var s in old) _db.UserSessions.Remove(s);
        if (old.Count > 0) await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Removed {Count} expired sessions", old.Count);
        return old.Count;
    }
}
