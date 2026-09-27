using Platform.Audit;
using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Workflow;

/// <summary>
/// Versioned workflow definition (FR-ADM-001, SRS §27). Only one Approved version per code is active;
/// instances keep a snapshot of the steps that were active when they started.
/// </summary>
public class WorkflowDefinition : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public int DefinitionVersion { get; set; } = 1;
    public ConfigStatus Status { get; set; } = ConfigStatus.Draft;
    public string? Description { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public string? ActivatedBy { get; set; }
    public ICollection<WorkflowStepDefinition> Steps { get; set; } = new List<WorkflowStepDefinition>();
}

/// <summary>
/// One approval step. The step applies only when the transaction value is at least MinimumValue
/// (value-based routing). RequiredRole decides who may act; AuthorityType enforces delegation limits.
/// </summary>
public class WorkflowStepDefinition : Entity
{
    public Guid DefinitionId { get; set; }
    public int StepOrder { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string RequiredRole { get; set; } = default!;
    public string? AuthorityType { get; set; }
    public decimal? MinimumValue { get; set; }

    /// <summary>Upper bound (inclusive) of the value band this step applies to; null = no upper bound (value-banded delegation tiers).</summary>
    public decimal? MaximumValue { get; set; }
    public int SlaHours { get; set; } = 72;
    public string? EscalationRole { get; set; }
}

public enum WorkflowState
{
    InProgress,
    Approved,
    Rejected,
    Returned,
    Cancelled
}

public class WorkflowInstance : AuditableEntity
{
    public Guid DefinitionId { get; set; }
    public string DefinitionCode { get; set; } = default!;
    public int DefinitionVersion { get; set; }
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string? EntityReference { get; set; }
    public string? Title { get; set; }
    public decimal? TransactionValue { get; set; }
    public Guid? ProjectId { get; set; }
    public int CurrentStepOrder { get; set; }
    public WorkflowState State { get; set; } = WorkflowState.InProgress;
    public DateTime StartedAtUtc { get; set; }
    public Guid? StartedByUserId { get; set; }
    public string? StartedBy { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>JSON snapshot of the applicable steps at start (the definition version is retained).</summary>
    public string StepsSnapshotJson { get; set; } = "[]";
    public ICollection<WorkflowTask> Tasks { get; set; } = new List<WorkflowTask>();
}

public enum TaskDecision
{
    Pending,
    Approved,
    Rejected,
    Returned,
    Cancelled
}

/// <summary>An approval task/decision (SRS entity "Approval"): step, approver, decision and time.</summary>
public class WorkflowTask : Entity
{
    public Guid InstanceId { get; set; }
    public int StepOrder { get; set; }
    public string StepCode { get; set; } = default!;
    public string StepName { get; set; } = default!;
    public string AssignedRole { get; set; } = default!;
    public string? AuthorityType { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime DueAtUtc { get; set; }
    public TaskDecision Decision { get; set; } = TaskDecision.Pending;
    public Guid? DecidedByUserId { get; set; }
    public string? DecidedBy { get; set; }
    public Guid? OnBehalfOfUserId { get; set; }
    public string? OnBehalfOfName { get; set; }
    public string? Comment { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public int EscalationLevel { get; set; }
    public DateTime? EscalatedAtUtc { get; set; }
    public string? EscalatedToRole { get; set; }
}

/// <summary>Delegated authority by role or user, amount, transaction type and effective dates (FR-ADM-002, BR-007).</summary>
public class Delegation : AuditableEntity
{
    public string AuthorityType { get; set; } = default!;
    public string? RoleCode { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public decimal MaxAmount { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string? PolicyReference { get; set; }

    public bool IsEffectiveOn(DateOnly date) => IsActive && EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo >= date);
}

/// <summary>Time-bound acting/substitute approver (FR-ADM-003). History is retained (revoked, never deleted).</summary>
public class Substitution : AuditableEntity
{
    public Guid PrincipalUserId { get; set; }
    public string PrincipalName { get; set; } = default!;
    public Guid SubstituteUserId { get; set; }
    public string SubstituteName { get; set; } = default!;
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public string Reason { get; set; } = default!;
    public bool IsRevoked { get; set; }

    public bool IsActiveAt(DateTime utc) => !IsRevoked && FromUtc <= utc && ToUtc >= utc;
}

public enum SodMode
{
    Block,
    Escalate
}

/// <summary>
/// Segregation-of-duties rule (SEC-005, BR-008): the same user may not perform both actions on the
/// same transaction. Mode decides whether the second action is blocked or routed for exception approval.
/// </summary>
public class SodRule : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string FirstAction { get; set; } = default!;
    public string SecondAction { get; set; } = default!;
    public SodMode Mode { get; set; } = SodMode.Block;
    public bool IsActive { get; set; } = true;
}

/// <summary>Append-only ledger of who performed which material action on which transaction (used for SoD checks).</summary>
[NotAudited]
public class TransactionAction : Entity
{
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string ActionCode { get; set; } = default!;
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid? ProjectId { get; set; }
}

/// <summary>Escalation event log (FR-ADM-005, FR-EXE-008, FR-ME-006, FR-RSK-004).</summary>
public class EscalationEvent : Entity
{
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string? EntityReference { get; set; }
    public int Level { get; set; }
    public string EscalatedToRole { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public DateTime OccurredAtUtc { get; set; }
}
