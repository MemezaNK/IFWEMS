using Platform.Audit;

namespace Teta.Ippcms.Domain.Common;

/// <summary>GUID technical key; human-readable business numbers are separate immutable properties (SRS §25).</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

/// <summary>
/// Core business entity metadata required by SRS §7: created/updated timestamps and user, plus a
/// version number used as an optimistic concurrency token (NFR-004). Populated automatically on save.
/// </summary>
public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Incremented on every update; concurrency token preventing lost updates.</summary>
    public long Version { get; set; }
}

/// <summary>Entities owned by a project, used for row-level (project scope) security (SEC-004).</summary>
public interface IProjectScoped
{
    Guid? ScopeProjectId { get; }
}

/// <summary>Standard polymorphic parent types for evidence, documents, comments, risks and actions.</summary>
public static class ParentTypes
{
    public const string Project = "Project";
    public const string Programme = "Programme";
    public const string Contract = "Contract";
    public const string Deliverable = "Deliverable";
    public const string Milestone = "Milestone";
    public const string Procurement = "Procurement";
    public const string Bid = "Bid";
    public const string Invoice = "Invoice";
    public const string PerformanceResult = "PerformanceResult";
    public const string MonitoringVisit = "MonitoringVisit";
    public const string Finding = "Finding";
    public const string CorrectiveAction = "CorrectiveAction";
    public const string AuditFinding = "AuditFinding";
    public const string Beneficiary = "Beneficiary";
    public const string ContractBreach = "ContractBreach";
    public const string ChangeRequest = "ChangeRequest";
    public const string Risk = "Risk";
    public const string Issue = "Issue";
    public const string Publication = "Publication";
    public const string Supplier = "Supplier";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Project, Programme, Contract, Deliverable, Milestone, Procurement, Bid, Invoice, PerformanceResult,
        MonitoringVisit, Finding, CorrectiveAction, AuditFinding, Beneficiary, ContractBreach, ChangeRequest,
        Risk, Issue, Publication, Supplier
    };
}

/// <summary>Generic approval state for records that pass through a workflow.</summary>
public enum ApprovalState
{
    Draft,
    Pending,
    Approved,
    Rejected,
    Cancelled
}

/// <summary>Lifecycle for controlled configuration (rules, thresholds, workflows): changes need approval (FR-BUD-008, BR-012).</summary>
public enum ConfigStatus
{
    Draft,
    PendingApproval,
    Approved,
    Retired
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}

public enum ActionStatus
{
    Open,
    InProgress,
    Completed,
    Cancelled
}

/// <summary>Immutable number sequence per prefix and year, e.g. PRJ-2026-0001.</summary>
[NotAudited]
public class NumberSequence : Entity
{
    public string Prefix { get; set; } = default!;
    public int Year { get; set; }
    public int NextValue { get; set; } = 1;
    public long Version { get; set; }
}
