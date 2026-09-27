using Platform.Audit;
using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Admin;

/// <summary>Controlled reference list value (FR-ADM-006): cost categories, funding sources, provinces, etc.</summary>
public class ReferenceDataItem : AuditableEntity
{
    public string Category { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>Business calendar holiday (FR-ADM-007).</summary>
public class PublicHoliday : AuditableEntity
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = default!;
}

/// <summary>Key/value configuration (thresholds, RAG rules, lead times) changeable without deployment (NFR-011).</summary>
public class SystemSetting : AuditableEntity
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? Description { get; set; }
    public string Category { get; set; } = "General";
}

/// <summary>Notification template (FR-ADM-004). Body uses {{Placeholder}} tokens.</summary>
public class NotificationTemplate : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Body { get; set; } = default!;
    public bool SendEmail { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

/// <summary>In-system notification with e-mail delivery status (FR-ADM-004).</summary>
[NotAudited]
public class Notification : Entity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? Link { get; set; }
    public string? Category { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public string? EmailStatus { get; set; }
    public string? SourceEntityType { get; set; }
    public Guid? SourceEntityId { get; set; }
}

/// <summary>Validated bulk import run with row-level error report (FR-ADM-009).</summary>
public class ImportJob : AuditableEntity
{
    public string ImportType { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public int TotalRows { get; set; }
    public int SucceededRows { get; set; }
    public int FailedRows { get; set; }
    public string Status { get; set; } = "Completed";
    public string? ErrorReportJson { get; set; }
    public bool ValidateOnly { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

/// <summary>Retention schedule per record class (NFR-013).</summary>
public class RetentionPolicy : AuditableEntity
{
    public string RecordClass { get; set; } = default!;
    public int RetentionYears { get; set; }
    public string DisposalAction { get; set; } = "Review";
    public string? LegalReference { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Transactional outbox message (SRS §34), processed by a background worker with retries.</summary>
[NotAudited]
public class OutboxMessage : Entity
{
    public DateTime OccurredAtUtc { get; set; }
    public string Type { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Stored response for an idempotent financial/duplicate-sensitive request (SRS §8.1, §26).</summary>
[NotAudited]
public class IdempotencyRecord : Entity
{
    public string Key { get; set; } = default!;
    public string RequestPath { get; set; } = default!;
    public Guid? UserId { get; set; }
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
