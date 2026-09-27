using Platform.Core;
using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Projects;

public class Portfolio : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? OwnerName { get; set; }
    public ICollection<Programme> Programmes { get; set; } = new List<Programme>();
}

public enum ProgrammeStatus
{
    Active,
    Closed
}

public class Programme : AuditableEntity
{
    public Guid PortfolioId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public ProgrammeStatus Status { get; set; } = ProgrammeStatus.Active;
}

public enum ProjectStatus
{
    Concept,
    BusinessCase,
    SubmittedForApproval,
    Approved,
    InExecution,
    OnHold,
    Closing,
    Closed,
    Cancelled,
    Rejected
}

public enum ProjectStage
{
    Initiation,
    Planning,
    Execution,
    CloseOut,
    BenefitReview,
    Completed
}

public enum HealthStatus
{
    NotAssessed,
    Green,
    Amber,
    Red
}

/// <summary>
/// Project master (FR-PPM-001/005). A concept gets a draft reference on registration; the immutable
/// Project ID (ProjectNumber) is generated only when the business case is approved (BR-001).
/// </summary>
public class Project : AuditableEntity
{
    public string DraftReference { get; set; } = default!;
    public string? ProjectNumber { get; private set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid ProgrammeId { get; set; }
    public Guid? SponsorUserId { get; set; }
    public string? SponsorName { get; set; }
    public Guid? ManagerUserId { get; set; }
    public string? ManagerName { get; set; }
    public string? BusinessOwner { get; set; }
    public string? OrgUnit { get; set; }
    public string? ProjectType { get; set; }
    public string? Province { get; set; }
    public string? District { get; set; }
    public string? Municipality { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Concept;
    public ProjectStage Stage { get; set; } = ProjectStage.Initiation;
    public DateOnly? PlannedStart { get; set; }
    public DateOnly? PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public decimal ApprovedBudget { get; set; }
    public HealthStatus Health { get; set; } = HealthStatus.NotAssessed;
    public string? HealthExplanation { get; set; }
    public DateTime? HealthCalculatedAtUtc { get; set; }
    public decimal? PriorityScore { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    /// <summary>Display reference: Project ID once active, otherwise the draft concept reference.</summary>
    public string Reference => ProjectNumber ?? DraftReference;

    public bool HasProjectId => ProjectNumber is not null;

    public bool IsClosedOrCancelled => Status is ProjectStatus.Closed or ProjectStatus.Cancelled or ProjectStatus.Rejected;

    /// <summary>Assigns the immutable Project ID on business-case approval (BR-001, FR-PPM-004/005).</summary>
    public void ActivateWithProjectId(string projectNumber, decimal approvedBudget, DateTime utcNow)
    {
        if (ProjectNumber is not null)
            throw new DomainException($"Project already has immutable Project ID {ProjectNumber}.", "FR-PPM-005");
        if (string.IsNullOrWhiteSpace(projectNumber))
            throw new DomainException("A Project ID is required.", "FR-PPM-005");
        if (approvedBudget < 0)
            throw new DomainException("Approved budget cannot be negative.", "FR-PPM-004");
        if (PlannedStart is { } s && PlannedEnd is { } e && e < s)
            throw new DomainException("Planned end date precedes planned start date.", "FR-PPM-002");

        ProjectNumber = projectNumber;
        ApprovedBudget = approvedBudget;
        Status = ProjectStatus.Approved;
        Stage = ProjectStage.Planning;
        ApprovedAtUtc = utcNow;
    }

    public void EnsureActive()
    {
        if (!HasProjectId || IsClosedOrCancelled)
            throw new DomainException($"Project {Reference} is not an approved, active project.", "BR-002");
    }
}

public class Workstream : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Lead { get; set; }
}

public enum BusinessCaseStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    Returned
}

/// <summary>Business case (FR-PPM-002). Mandatory fields are validated before submission.</summary>
public class BusinessCase : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string? Problem { get; set; }
    public string? Objectives { get; set; }
    public string? Options { get; set; }
    public string? Scope { get; set; }
    public string? Benefits { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? Risks { get; set; }
    public string? DeliveryModel { get; set; }
    public string? ExpectedBenefitMeasure { get; set; }
    public decimal? ExpectedBenefitValue { get; set; }
    public BusinessCaseStatus Status { get; set; } = BusinessCaseStatus.Draft;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionComment { get; set; }

    /// <summary>Returns the list of missing mandatory fields (empty when complete).</summary>
    public IReadOnlyList<string> MissingMandatoryFields()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(Problem)) missing.Add(nameof(Problem));
        if (string.IsNullOrWhiteSpace(Objectives)) missing.Add(nameof(Objectives));
        if (string.IsNullOrWhiteSpace(Options)) missing.Add(nameof(Options));
        if (string.IsNullOrWhiteSpace(Scope)) missing.Add(nameof(Scope));
        if (string.IsNullOrWhiteSpace(Benefits)) missing.Add(nameof(Benefits));
        if (EstimatedCost <= 0) missing.Add(nameof(EstimatedCost));
        if (string.IsNullOrWhiteSpace(Risks)) missing.Add(nameof(Risks));
        if (string.IsNullOrWhiteSpace(DeliveryModel)) missing.Add(nameof(DeliveryModel));
        return missing;
    }
}

/// <summary>Configurable prioritisation criterion (FR-PPM-003).</summary>
public class PrioritisationCriterion : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Category { get; set; } = "Strategic";
    public decimal Weight { get; set; }
    public int MaxScore { get; set; } = 5;
    public bool IsActive { get; set; } = true;
}

public class ProjectPriorityScore : Entity
{
    public Guid ProjectId { get; set; }
    public Guid CriterionId { get; set; }
    public decimal Score { get; set; }
    public string? Rationale { get; set; }
    public DateTime ScoredAtUtc { get; set; }
    public string? ScoredBy { get; set; }
}

public enum CharterStatus
{
    Draft,
    Approved
}

/// <summary>Project charter and governance structure (FR-PPM-007).</summary>
public class ProjectCharter : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string Purpose { get; set; } = default!;
    public string Scope { get; set; } = default!;
    public string GovernanceStructure { get; set; } = default!;
    public string? KeyMilestones { get; set; }
    public string? Assumptions { get; set; }
    public CharterStatus Status { get; set; } = CharterStatus.Draft;
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }
}

public enum StakeholderRole
{
    Sponsor,
    ProjectManager,
    TeamMember,
    BusinessOwner,
    SteeringCommittee,
    Stakeholder
}

/// <summary>Project role holder with effective dates (FR-PPM-008).</summary>
public class ProjectStakeholder : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = default!;
    public string? Organisation { get; set; }
    public StakeholderRole Role { get; set; }
    public string? Responsibility { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public bool IsEffectiveOn(DateOnly date) => EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo >= date);
}

/// <summary>Configured stage-gate criterion (FR-PPM-009). CheckCode enables automatic evaluation.</summary>
public class StageGateCriterion : AuditableEntity
{
    public ProjectStage Stage { get; set; }
    public string Code { get; set; } = default!;
    public string Description { get; set; } = default!;
    public bool IsMandatory { get; set; } = true;
    public string? CheckCode { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum GateDecision
{
    Pending,
    Passed,
    Failed
}

public class StageGateReview : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public ProjectStage FromStage { get; set; }
    public ProjectStage ToStage { get; set; }
    public GateDecision Decision { get; set; } = GateDecision.Pending;
    public string? Comment { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
    public ICollection<StageGateCheck> Checks { get; set; } = new List<StageGateCheck>();
}

public class StageGateCheck : Entity
{
    public Guid ReviewId { get; set; }
    public Guid CriterionId { get; set; }
    public string CriterionCode { get; set; } = default!;
    public string CriterionDescription { get; set; } = default!;
    public bool IsMandatory { get; set; }
    public bool IsMet { get; set; }
    public bool AutoEvaluated { get; set; }
    public string? Evidence { get; set; }
}

/// <summary>Append-only project status/stage history (FR-PPM-010).</summary>
public class ProjectStatusHistory : Entity
{
    public Guid ProjectId { get; set; }
    public ProjectStatus FromStatus { get; set; }
    public ProjectStatus ToStatus { get; set; }
    public ProjectStage? FromStage { get; set; }
    public ProjectStage? ToStage { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string? ChangedBy { get; set; }
    public string? Reason { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
}
