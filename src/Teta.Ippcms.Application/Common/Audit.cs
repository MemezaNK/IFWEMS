using System.Text.Json;
using Platform.Audit;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Application.Common;

/// <summary>Seals/validates audit rows. Field order here is the canonical sealing order.</summary>
public static class AuditLogSealing
{
    public static string Seal(AuditSealer sealer, AuditLog log) => sealer.Seal(Fields(log));

    public static bool Verify(AuditSealer sealer, AuditLog log) => sealer.Verify(log.Seal, Fields(log));

    private static object?[] Fields(AuditLog log) => new object?[]
    {
        log.OccurredAtUtc, log.UserId, log.Username, log.CorrelationId, log.SessionId, log.Module, log.EntityType,
        log.EntityId, log.Action, log.OldValues, log.NewValues, log.WorkflowStep, log.Reason, log.SourceIp
    };

    /// <summary>Maps an entity CLR namespace (Teta.Ippcms.Domain.{Module}) to the audit module name.</summary>
    public static string ModuleOf(Type entityType)
    {
        var ns = entityType.Namespace ?? string.Empty;
        var last = ns[(ns.LastIndexOf('.') + 1)..];
        return last switch
        {
            "Scm" => "Procurement",
            "Identity" => "Security",
            _ => last
        };
    }
}

/// <summary>
/// Writes explicit audit events for material actions that are not simple entity changes: logins,
/// workflow decisions, overrides/exceptions (with reason), exports and security events (SRS §14).
/// </summary>
public interface IAuditWriter
{
    void Write(string module, string entityType, string? entityId, string action, object? details = null,
        string? reason = null, string? workflowStep = null);
}

public sealed class AuditWriter : IAuditWriter
{
    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly AuditSealer _sealer;

    public AuditWriter(ITetaDbContext db, ICurrentUser user, IClock clock, AuditSealer sealer)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _sealer = sealer;
    }

    public void Write(string module, string entityType, string? entityId, string action, object? details = null,
        string? reason = null, string? workflowStep = null)
    {
        var log = new AuditLog
        {
            OccurredAtUtc = _clock.UtcNow,
            UserId = _user.UserId,
            Username = _user.Username ?? "system",
            CorrelationId = _user.CorrelationId ?? Guid.NewGuid().ToString("N"),
            SessionId = _user.SessionId,
            Module = module,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            NewValues = details is null ? null : JsonSerializer.Serialize(details),
            Reason = reason,
            WorkflowStep = workflowStep,
            SourceIp = _user.IpAddress,
            UserAgent = _user.UserAgent
        };
        log.Seal = AuditLogSealing.Seal(_sealer, log);
        _db.AuditLogs.Add(log);
    }
}
