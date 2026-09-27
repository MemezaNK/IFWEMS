using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Reporting;

/// <summary>Controlled Board/committee reporting pack dataset with period and version (FR-REP-003).</summary>
public class BoardReportPack : AuditableEntity
{
    public string Number { get; set; } = default!;
    public string Period { get; set; } = default!;
    public int PackVersion { get; set; } = 1;
    public string Title { get; set; } = default!;
    public ApprovalState Status { get; set; } = ApprovalState.Draft;
    public string DatasetJson { get; set; } = default!;
    public DateTime GeneratedAtUtc { get; set; }
    public string? GeneratedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }
}

/// <summary>Scheduled report delivery (FR-REP-006). Can be disabled/changed by its owner.</summary>
public class ReportSchedule : AuditableEntity
{
    public string ReportCode { get; set; } = default!;
    public string Format { get; set; } = "csv";
    public string Frequency { get; set; } = "Weekly";
    public string Recipients { get; set; } = default!;
    public bool IsEnabled { get; set; } = true;
    public Guid OwnerUserId { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
    public DateTime? NextRunAtUtc { get; set; }
    public string? LastRunStatus { get; set; }
}

public enum DataQualityStatus
{
    Open,
    Resolved,
    Closed,
    Ignored
}

/// <summary>Detected data-quality/unreconciled item with an owner who can resolve or close it (FR-REP-010).</summary>
public class DataQualityIssue : AuditableEntity
{
    public string RuleCode { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public Guid? EntityId { get; set; }
    public string? EntityReference { get; set; }
    public Severity Severity { get; set; } = Severity.Medium;
    public DataQualityStatus Status { get; set; } = DataQualityStatus.Open;
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public string? Resolution { get; set; }
    public DateTime DetectedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
