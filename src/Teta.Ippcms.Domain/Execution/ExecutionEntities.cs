using Platform.Core;
using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Execution;

public enum WbsType
{
    Workstream,
    Deliverable,
    Milestone,
    Activity,
    Task
}

public enum WorkStatus
{
    NotStarted,
    InProgress,
    Completed,
    Blocked,
    Cancelled
}

/// <summary>
/// One node of the project work breakdown structure (FR-EXE-001): workstream → deliverable →
/// milestone → activity → task. Milestones and tasks from the SRS data model are WBS elements of
/// the corresponding type. Baseline dates are frozen by an approved schedule baseline (FR-EXE-002).
/// </summary>
public class WbsElement : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ParentId { get; set; }
    public WbsType Type { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public DateOnly? PlannedStart { get; set; }
    public DateOnly? PlannedEnd { get; set; }
    public DateOnly? BaselineStart { get; set; }
    public DateOnly? BaselineEnd { get; set; }
    public DateOnly? ForecastEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public decimal PercentComplete { get; set; }
    public decimal Weight { get; set; } = 1m;
    public bool IsCritical { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public bool EvidenceRequired { get; set; }
    public WorkStatus Status { get; set; } = WorkStatus.NotStarted;
    public int SortOrder { get; set; }

    public bool IsOverdue(DateOnly today) =>
        Status is not (WorkStatus.Completed or WorkStatus.Cancelled) && PlannedEnd is { } end && end < today;

    /// <summary>Days late against baseline (or plan): positive = behind schedule.</summary>
    public int ScheduleVarianceDays(DateOnly today)
    {
        var reference = BaselineEnd ?? PlannedEnd;
        if (reference is null) return 0;

        DateOnly compare;
        if (ActualEnd is { } actual) compare = actual;
        else if (ForecastEnd is { } forecast) compare = forecast;
        else if (Status == WorkStatus.Completed) compare = reference.Value;
        else compare = today > reference.Value ? today : reference.Value;

        return compare.DayNumber - reference.Value.DayNumber;
    }

    public void RecordProgress(decimal percent, DateOnly? forecastEnd, DateOnly? actualStart, DateOnly? actualEnd)
    {
        if (percent is < 0 or > 100) throw new DomainException("Percent complete must be between 0 and 100.", "FR-EXE-005");
        if (actualEnd is { } ae && actualStart is { } asd && ae < asd)
            throw new DomainException("Actual end cannot precede actual start.", "FR-EXE-005");
        PercentComplete = percent;
        if (forecastEnd.HasValue) ForecastEnd = forecastEnd;
        if (actualStart.HasValue) ActualStart = actualStart;
        if (actualEnd.HasValue) ActualEnd = actualEnd;
        Status = percent >= 100 ? WorkStatus.Completed : percent > 0 ? WorkStatus.InProgress : Status;
    }
}

public enum DependencyType
{
    FS,
    SS,
    FF,
    SF
}

/// <summary>Schedule dependency between WBS elements (FR-EXE-003).</summary>
public class WbsDependency : Entity
{
    public Guid ProjectId { get; set; }
    public Guid PredecessorId { get; set; }
    public Guid SuccessorId { get; set; }
    public DependencyType Type { get; set; } = DependencyType.FS;
    public int LagDays { get; set; }
}

/// <summary>Immutable approved schedule baseline snapshot; revisions add new baselines (FR-EXE-002, BR-005).</summary>
public class ScheduleBaseline : Entity
{
    public Guid ProjectId { get; set; }
    public int BaselineNumber { get; set; }
    public DateTime ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }
    public string SnapshotJson { get; set; } = default!;
    public Guid? ChangeRequestId { get; set; }
    public string? Reason { get; set; }
    public DateOnly? PlannedEnd { get; set; }
    public decimal BudgetAtBaseline { get; set; }
}

/// <summary>Progress history record (FR-EXE-005).</summary>
public class ProgressUpdate : Entity
{
    public Guid WbsElementId { get; set; }
    public Guid ProjectId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public decimal PercentComplete { get; set; }
    public DateOnly? ForecastEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public string? Comment { get; set; }
    public string? RecordedBy { get; set; }
}

/// <summary>Internal or external resource assigned to a WBS element (FR-EXE-007).</summary>
public class ResourceAssignment : AuditableEntity
{
    public Guid WbsElementId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public string ResourceName { get; set; } = default!;
    public bool IsExternal { get; set; }
    public string Role { get; set; } = default!;
    public decimal AllocationPercent { get; set; }
}

public enum IssueStatus
{
    Open,
    InProgress,
    Escalated,
    Resolved,
    Closed
}

/// <summary>Project issue register entry (FR-EXE-008).</summary>
public class Issue : AuditableEntity
{
    public string Number { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public Severity Severity { get; set; } = Severity.Medium;
    public Guid? OwnerUserId { get; set; }
    public string OwnerName { get; set; } = default!;
    public string? Action { get; set; }
    public DateOnly? DueDate { get; set; }
    public IssueStatus Status { get; set; } = IssueStatus.Open;
    public int EscalationLevel { get; set; }
    public DateTime? LastEscalatedAtUtc { get; set; }
    public string? Resolution { get; set; }

    public bool IsOpen => Status is IssueStatus.Open or IssueStatus.InProgress or IssueStatus.Escalated;
}

public enum DependencyDirection
{
    Internal,
    External
}

public enum DependencyStatus
{
    Open,
    OnTrack,
    AtRisk,
    Late,
    Resolved
}

/// <summary>Project dependency register (FR-EXE-009).</summary>
public class ProjectDependency : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public string Description { get; set; } = default!;
    public DependencyDirection Direction { get; set; }
    public string DependsOn { get; set; } = default!;
    public string? Impact { get; set; }
    public DateOnly? NeededBy { get; set; }
    public string? OwnerName { get; set; }
    public DependencyStatus Status { get; set; } = DependencyStatus.Open;
}

public enum ChangeType
{
    Scope,
    Cost,
    Schedule,
    Benefit
}

/// <summary>Scope/cost/schedule/benefit change request; approval creates a controlled baseline revision (FR-EXE-010).</summary>
public class ChangeRequest : AuditableEntity
{
    public string Number { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid? ContractId { get; set; }
    public ChangeType Type { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Justification { get; set; } = default!;
    public decimal CostImpact { get; set; }
    public int ScheduleImpactDays { get; set; }
    public DateOnly? ProposedEndDate { get; set; }
    public string? BenefitImpact { get; set; }
    public string? ContractImpact { get; set; }
    public Guid? BudgetLineId { get; set; }
    public ApprovalState Status { get; set; } = ApprovalState.Draft;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
    public Guid? ResultingBaselineId { get; set; }
    public Guid? ResultingVariationId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
}

/// <summary>Calculated health snapshot with its underlying measures (FR-EXE-011).</summary>
public class ProjectHealthSnapshot : Entity
{
    public Guid ProjectId { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
    public string Overall { get; set; } = default!;
    public int ScheduleScore { get; set; }
    public int CostScore { get; set; }
    public int RiskScore { get; set; }
    public int DeliveryScore { get; set; }
    public int ScheduleVarianceDays { get; set; }
    public decimal CostVariancePercent { get; set; }
    public int CriticalOpenRisks { get; set; }
    public int HighOpenRisks { get; set; }
    public int OverdueMilestones { get; set; }
    public int OverdueCriticalMilestones { get; set; }
    public string Explanation { get; set; } = default!;
}

/// <summary>Controlled comment / activity note on any record (FR-EXE-013).</summary>
public class Comment : Entity
{
    public string ParentType { get; set; } = default!;
    public Guid ParentId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Text { get; set; } = default!;
    public Guid? AuthorUserId { get; set; }
    public string AuthorName { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
}

public enum ClosureStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected
}

/// <summary>Project closure: completion, reconciliation, handover and lessons learned (FR-EXE-014, BR-010).</summary>
public class ProjectClosure : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public string LessonsLearned { get; set; } = default!;
    public string HandoverNotes { get; set; } = default!;
    public bool FinancialReconciliationConfirmed { get; set; }
    public bool DocumentationComplete { get; set; }
    public string? ChecklistJson { get; set; }
    public string? ApprovedExceptionReference { get; set; }
    public ClosureStatus Status { get; set; } = ClosureStatus.Draft;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
}

public enum BenefitReviewStatus
{
    Scheduled,
    Completed,
    Cancelled
}

/// <summary>Post-implementation benefit review compared with the business case (FR-EXE-015).</summary>
public class BenefitReview : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public string ExpectedBenefit { get; set; } = default!;
    public decimal? ExpectedValue { get; set; }
    public string? ActualBenefit { get; set; }
    public decimal? ActualValue { get; set; }
    public string? Findings { get; set; }
    public BenefitReviewStatus Status { get; set; } = BenefitReviewStatus.Scheduled;
    public DateOnly? CompletedDate { get; set; }

    public decimal? RealisationPercent =>
        ExpectedValue is > 0 && ActualValue is { } a ? Math.Round(a / ExpectedValue.Value * 100m, 1) : null;
}
