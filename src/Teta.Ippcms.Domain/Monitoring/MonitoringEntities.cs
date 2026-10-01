using Platform.Audit;
using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Monitoring;

/// <summary>Monitoring &amp; evaluation plan linked to a project (FR-ME-001).</summary>
public class MePlan : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public string Description { get; set; } = default!;
    public string Frequency { get; set; } = "Quarterly";
    public string Methods { get; set; } = default!;
    public string? Indicators { get; set; }
    public Guid? ResponsibleOfficerUserId { get; set; }
    public string ResponsibleOfficerName { get; set; } = default!;
    public DateOnly? NextVisitDue { get; set; }
}

public enum TemplateStatus
{
    Draft,
    Published,
    Retired
}

/// <summary>
/// Configurable desktop/site monitoring form (FR-ME-002). FieldsJson is a list of
/// {key,label,type,required,options}. Publishing freezes a version; visits keep the version they used.
/// </summary>
public class MonitoringTemplate : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ProjectType { get; set; }
    public int TemplateVersion { get; set; } = 1;
    public TemplateStatus Status { get; set; } = TemplateStatus.Draft;
    public string FieldsJson { get; set; } = "[]";
    public DateTime? PublishedAtUtc { get; set; }
}

public enum VisitType
{
    Desktop,
    Site
}

public enum VisitStatus
{
    Scheduled,
    Completed,
    Cancelled
}

/// <summary>Scheduled/completed monitoring visit captured on a mobile-responsive form (FR-ME-003).</summary>
public class MonitoringVisit : AuditableEntity
{
    public string Number { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid? TemplateId { get; set; }
    public int? TemplateVersion { get; set; }
    public string? TemplateFieldsSnapshotJson { get; set; }
    public VisitType Type { get; set; } = VisitType.Site;
    public DateOnly ScheduledDate { get; set; }
    public DateOnly? VisitDate { get; set; }
    public string? Officials { get; set; }
    public string? Location { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? ResponsesJson { get; set; }
    public string? Summary { get; set; }
    public string? Outcome { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Scheduled;
    public Guid? ProviderSupplierId { get; set; }
}

public enum FindingStatus
{
    Open,
    ActionInProgress,
    Closed
}

/// <summary>Monitoring finding with severity and root cause (FR-ME-005).</summary>
public class Finding : AuditableEntity
{
    public string Number { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid? VisitId { get; set; }
    public string Source { get; set; } = "Monitoring";
    public string Description { get; set; } = default!;
    public Severity Severity { get; set; } = Severity.Medium;
    public string? RootCause { get; set; }
    public FindingStatus Status { get; set; } = FindingStatus.Open;
    public DateTime? ClosedAtUtc { get; set; }
}

/// <summary>
/// Corrective action for a monitoring finding, audit finding or contract breach (FR-ME-006, FR-RSK-006).
/// Closure requires closure evidence; overdue actions escalate.
/// </summary>
public class CorrectiveAction : AuditableEntity
{
    public string Number { get; set; } = default!;
    public string ParentType { get; set; } = default!;
    public Guid ParentId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Description { get; set; } = default!;
    public Guid? OwnerUserId { get; set; }
    public string OwnerName { get; set; } = default!;
    public DateOnly DueDate { get; set; }
    public ActionStatus Status { get; set; } = ActionStatus.Open;
    public string? ClosureNotes { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int EscalationLevel { get; set; }
    public DateTime? LastEscalatedAtUtc { get; set; }

    public bool IsOverdue(DateOnly today) => Status is ActionStatus.Open or ActionStatus.InProgress && DueDate < today;
}

public enum BeneficiaryStatus
{
    Registered,
    Enrolled,
    Participating,
    Completed,
    DroppedOut,
    Placed
}

/// <summary>
/// Learner/beneficiary record (FR-ME-007/008/009). The identity number is stored encrypted, with a
/// keyed hash for duplicate detection and a masked form for display (POPIA minimisation, SEC-011).
/// </summary>
public class Beneficiary : AuditableEntity
{
    public string Number { get; set; } = default!;
    public Guid ProjectId { get; set; }

    [SensitiveData]
    public string IdentifierEncrypted { get; set; } = default!;
    public string IdentifierHash { get; set; } = default!;
    public string IdentifierMasked { get; set; } = default!;
    public string IdentifierType { get; set; } = "SAID";

    [SensitiveData]
    public string FirstName { get; set; } = default!;

    [SensitiveData]
    public string LastName { get; set; } = default!;
    public string? Gender { get; set; }
    public int? BirthYear { get; set; }
    public string? Province { get; set; }
    public string? District { get; set; }
    public string Intervention { get; set; } = default!;
    public Guid? ProviderSupplierId { get; set; }
    public string? FundingSource { get; set; }
    public BeneficiaryStatus Status { get; set; } = BeneficiaryStatus.Registered;
    public bool PotentialDuplicate { get; set; }
    public string? DuplicateNote { get; set; }
    public bool ConsentObtained { get; set; }

    /// <summary>Intake group label (e.g. "Cohort 1") used for cohort-level delivery reporting (FR-REP learner delivery).</summary>
    public string? Cohort { get; set; }
}

public class BeneficiaryStatusHistory : Entity
{
    public Guid BeneficiaryId { get; set; }
    public BeneficiaryStatus FromStatus { get; set; }
    public BeneficiaryStatus ToStatus { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string? ChangedBy { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// TETA-configurable delivery targets for a learnership/beneficiary project, used solely to compute
/// contract-vs-actual KPIs on the Learner Delivery &amp; Monitoring Report. One row per project; absence
/// of a row means targets have not yet been approved/configured (the report shows "Not configured").
/// </summary>
public class LearnerDeliveryTarget : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public int ContractedLearners { get; set; }
    public int LearnersDueForCompletion { get; set; }
    public int MonitoringVisitsPlanned { get; set; }
    public int WithdrawalTolerancePercent { get; set; }
}
