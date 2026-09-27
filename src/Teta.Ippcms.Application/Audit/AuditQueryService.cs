using Microsoft.EntityFrameworkCore;
using Platform.Audit;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Application.Audit;

public sealed record AuditQuery(string? Module, string? EntityType, string? EntityId, string? Username, string? Action, DateTime? FromUtc, DateTime? ToUtc,
    string? CorrelationId, int Page = 1, int PageSize = 50);
public sealed record AuditLogDto(long Id, DateTime OccurredAtUtc, Guid? UserId, string? Username, string CorrelationId, string? SessionId, string Module,
    string EntityType, string? EntityId, string Action, string? OldValues, string? NewValues, string? WorkflowStep, string? Reason, string? SourceIp,
    bool SealValid);
public sealed record IntegrityReportDto(DateTime CheckedAtUtc, long FromId, long ToId, int Checked, int Invalid, IReadOnlyList<long> InvalidIds, bool Intact);
public sealed record TrailStepDto(DateTime OccurredAtUtc, string Source, string Description, string? Actor, string? Details);
public sealed record EntityTrailDto(string EntityType, Guid EntityId, IReadOnlyList<TrailStepDto> Steps, IReadOnlyList<AuditLogDto> AuditRecords);

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> SearchAsync(AuditQuery query, CancellationToken ct);
    Task<IntegrityReportDto> VerifyIntegrityAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
    Task<EntityTrailDto> EntityTrailAsync(string entityType, Guid entityId, CancellationToken ct);
}

/// <summary>
/// Read-only audit trail access for Internal Audit / Risk (SRS §14, §25.2): filtered search, seal
/// verification (tamper evidence, SEC-010) and end-to-end reconstruction of a transaction (NFR-015).
/// Viewing the audit trail is itself audited.
/// </summary>
public sealed class AuditQueryService : IAuditQueryService
{
    private readonly ITetaDbContext _db;
    private readonly AuditSealer _sealer;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public AuditQueryService(ITetaDbContext db, AuditSealer sealer, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _sealer = sealer;
        _clock = clock;
        _audit = audit;
    }

    private AuditLogDto ToDto(AuditLog a) => new(a.Id, a.OccurredAtUtc, a.UserId, a.Username, a.CorrelationId, a.SessionId, a.Module, a.EntityType, a.EntityId,
        a.Action, a.OldValues, a.NewValues, a.WorkflowStep, a.Reason, a.SourceIp, AuditLogSealing.Verify(_sealer, a));

    public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 500);
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Module)) query = query.Where(a => a.Module == q.Module);
        if (!string.IsNullOrWhiteSpace(q.EntityType)) query = query.Where(a => a.EntityType == q.EntityType);
        if (!string.IsNullOrWhiteSpace(q.EntityId)) query = query.Where(a => a.EntityId == q.EntityId);
        if (!string.IsNullOrWhiteSpace(q.Username)) query = query.Where(a => a.Username != null && a.Username.Contains(q.Username));
        if (!string.IsNullOrWhiteSpace(q.Action)) query = query.Where(a => a.Action == q.Action);
        if (!string.IsNullOrWhiteSpace(q.CorrelationId)) query = query.Where(a => a.CorrelationId == q.CorrelationId);
        if (q.FromUtc is { } from) query = query.Where(a => a.OccurredAtUtc >= from);
        if (q.ToUtc is { } to) query = query.Where(a => a.OccurredAtUtc <= to);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(a => a.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        _audit.Write("Audit", "AuditLog", null, "View",
            new { q.Module, q.EntityType, q.EntityId, q.Username, q.Action, q.FromUtc, q.ToUtc, Page = page, Returned = rows.Count });
        await _db.SaveChangesAsync(ct);
        return new PagedResult<AuditLogDto>(rows.Select(ToDto).ToList(), total, page, size);
    }

    public async Task<IntegrityReportDto> VerifyIntegrityAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (fromUtc is { } f) query = query.Where(a => a.OccurredAtUtc >= f);
        if (toUtc is { } t) query = query.Where(a => a.OccurredAtUtc <= t);

        var invalid = new List<long>();
        var checkedCount = 0;
        long minId = 0, maxId = 0, lastId = 0;
        const int batch = 2000;
        while (true)
        {
            var from = lastId;
            var rows = await query.Where(a => a.Id > from).OrderBy(a => a.Id).Take(batch).ToListAsync(ct);
            if (rows.Count == 0) break;
            if (minId == 0) minId = rows[0].Id;
            foreach (var row in rows)
            {
                checkedCount++;
                if (!AuditLogSealing.Verify(_sealer, row)) invalid.Add(row.Id);
            }
            lastId = maxId = rows[^1].Id;
            if (rows.Count < batch) break;
        }

        _audit.Write("Audit", "AuditLog", null, "VerifyIntegrity", new { fromUtc, toUtc, Checked = checkedCount, Invalid = invalid.Count });
        await _db.SaveChangesAsync(ct);
        return new IntegrityReportDto(_clock.UtcNow, minId, maxId, checkedCount, invalid.Count, invalid.Take(500).ToList(), invalid.Count == 0);
    }

    public async Task<EntityTrailDto> EntityTrailAsync(string entityType, Guid entityId, CancellationToken ct)
    {
        var id = entityId.ToString();
        var instances = await _db.WorkflowInstances.AsNoTracking().Where(i => i.EntityType == entityType && i.EntityId == entityId).ToListAsync(ct);
        var instanceIds = instances.Select(i => i.Id).ToList();
        var tasks = await _db.WorkflowTasks.AsNoTracking().Where(t => instanceIds.Contains(t.InstanceId)).ToListAsync(ct);
        var relatedIds = instanceIds.Select(i => i.ToString()).Concat(tasks.Select(t => t.Id.ToString())).Append(id).ToList();

        var logs = await _db.AuditLogs.AsNoTracking().Where(a => a.EntityId != null && relatedIds.Contains(a.EntityId)).OrderBy(a => a.Id).ToListAsync(ct);
        var actions = await _db.TransactionActions.AsNoTracking().Where(a => a.EntityType == entityType && a.EntityId == entityId).ToListAsync(ct);
        var escalations = await _db.EscalationEvents.AsNoTracking().Where(e => e.EntityId == entityId || instanceIds.Contains(e.EntityId)).ToListAsync(ct);
        var documents = await _db.Documents.AsNoTracking().Where(d => d.ParentType == entityType && d.ParentId == entityId).ToListAsync(ct);
        var evidence = await _db.Evidence.AsNoTracking().Where(e => e.ParentType == entityType && e.ParentId == entityId).ToListAsync(ct);

        var steps = new List<TrailStepDto>();
        steps.AddRange(logs.Select(l => new TrailStepDto(l.OccurredAtUtc, "Audit", $"{l.Action} {l.EntityType}", l.Username,
            l.WorkflowStep is null ? l.Reason : $"Step {l.WorkflowStep}: {l.Reason}")));
        steps.AddRange(actions.Select(a => new TrailStepDto(a.OccurredAtUtc, "Action", a.ActionCode, a.Username, null)));
        foreach (var instance in instances)
        {
            steps.Add(new TrailStepDto(instance.StartedAtUtc, "Workflow", $"Workflow {instance.DefinitionCode} v{instance.DefinitionVersion} started", instance.StartedBy,
                instance.TransactionValue is { } v ? $"Value {v:N2}" : null));
            steps.AddRange(tasks.Where(t => t.InstanceId == instance.Id && t.DecidedAtUtc != null).Select(t => new TrailStepDto(t.DecidedAtUtc!.Value, "Workflow",
                $"{t.StepName}: {t.Decision}", t.OnBehalfOfName is null ? t.DecidedBy : $"{t.DecidedBy} (for {t.OnBehalfOfName})", t.Comment)));
            if (instance.CompletedAtUtc is { } done)
                steps.Add(new TrailStepDto(done, "Workflow", $"Workflow {instance.State}", null, null));
        }
        steps.AddRange(escalations.Select(e => new TrailStepDto(e.OccurredAtUtc, "Escalation", $"Escalated to {e.EscalatedToRole} (level {e.Level})", null, e.Reason)));
        steps.AddRange(documents.Select(d => new TrailStepDto(d.CreatedAtUtc, "Document", $"{d.DocumentType} '{d.Title}' v{d.DocumentVersion} uploaded", d.CreatedBy,
            d.FileName)));
        steps.AddRange(evidence.Where(e => e.VerifiedAtUtc != null).Select(e => new TrailStepDto(e.VerifiedAtUtc!.Value, "Evidence",
            $"{e.EvidenceType} {e.VerificationStatus}", e.VerifiedBy, e.VerificationComment)));

        _audit.Write("Audit", entityType, id, "ViewTrail");
        await _db.SaveChangesAsync(ct);
        return new EntityTrailDto(entityType, entityId, steps.OrderBy(s => s.OccurredAtUtc).ToList(), logs.Select(ToDto).ToList());
    }
}
