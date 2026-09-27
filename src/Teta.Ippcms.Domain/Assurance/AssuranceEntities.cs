using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Monitoring;

namespace Teta.Ippcms.Domain.Assurance;

/// <summary>Configurable risk rating band on the likelihood × impact score (FR-RSK-002).</summary>
public class RiskRatingBand : AuditableEntity
{
    public string Name { get; set; } = default!;
    public int MinScore { get; set; }
    public int MaxScore { get; set; }
    public string Colour { get; set; } = default!;
    public int SortOrder { get; set; }
}

public enum RiskStatus
{
    Open,
    Treating,
    Accepted,
    Closed
}

/// <summary>Project/contract/programme risk with inherent and residual ratings (FR-RSK-001, FR-CON-012).</summary>
public class Risk : AuditableEntity
{
    public string Number { get; set; } = default!;
    public string ParentType { get; set; } = default!;
    public Guid ParentId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = default!;
    public string Cause { get; set; } = default!;
    public string Event { get; set; } = default!;
    public string Consequence { get; set; } = default!;
    public string? Category { get; set; }
    public int InherentLikelihood { get; set; }
    public int InherentImpact { get; set; }
    public int InherentScore { get; set; }
    public string InherentRating { get; set; } = default!;
    public int ResidualLikelihood { get; set; }
    public int ResidualImpact { get; set; }
    public int ResidualScore { get; set; }
    public string ResidualRating { get; set; } = default!;
    public Guid? OwnerUserId { get; set; }
    public string OwnerName { get; set; } = default!;
    public DateOnly ReviewDate { get; set; }
    public RiskStatus Status { get; set; } = RiskStatus.Open;
    public DateTime? EscalatedAtUtc { get; set; }

    public bool IsOpen => Status is RiskStatus.Open or RiskStatus.Treating;
}

/// <summary>Control linked to a risk with its owner (FR-RSK-003).</summary>
public class RiskControl : AuditableEntity
{
    public Guid RiskId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string OwnerName { get; set; } = default!;
    public string Effectiveness { get; set; } = "NotAssessed";
    public DateOnly? LastAssessedOn { get; set; }
}

/// <summary>Control assessment history (FR-RSK-003).</summary>
public class ControlAssessment : Entity
{
    public Guid ControlId { get; set; }
    public DateOnly AssessedOn { get; set; }
    public string Effectiveness { get; set; } = default!;
    public string? Comment { get; set; }
    public string? AssessedBy { get; set; }
}

/// <summary>Risk treatment/mitigation action (FR-RSK-004).</summary>
public class RiskTreatment : AuditableEntity
{
    public Guid RiskId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Description { get; set; } = default!;
    public Guid? OwnerUserId { get; set; }
    public string OwnerName { get; set; } = default!;
    public DateOnly DueDate { get; set; }
    public ActionStatus Status { get; set; } = ActionStatus.Open;
    public DateTime? CompletedAtUtc { get; set; }
    public int EscalationLevel { get; set; }
    public DateTime? LastEscalatedAtUtc { get; set; }
}

/// <summary>Configurable compliance obligation (FR-RSK-005).</summary>
public class ComplianceObligation : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Source { get; set; } = default!;
    public string? Description { get; set; }
    public string Frequency { get; set; } = "Quarterly";
    public Guid? OwnerUserId { get; set; }
    public string OwnerName { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}

public enum AttestationResult
{
    Compliant,
    PartiallyCompliant,
    NonCompliant,
    NotApplicable
}

public class ComplianceAttestation : AuditableEntity
{
    public Guid ObligationId { get; set; }
    public string Period { get; set; } = default!;
    public AttestationResult Result { get; set; }
    public string? Comment { get; set; }
    public DateTime AttestedAtUtc { get; set; }
    public string AttestedBy { get; set; } = default!;
    public Guid? AttestedByUserId { get; set; }
}

public enum AuditSource
{
    InternalAudit,
    ExternalAudit,
    RiskReview,
    Other
}

/// <summary>Internal/external audit finding linked to a project or process (FR-RSK-006).</summary>
public class AuditFinding : AuditableEntity
{
    public string Number { get; set; } = default!;
    public Guid? ProjectId { get; set; }
    public string Process { get; set; } = default!;
    public AuditSource Source { get; set; }
    public string? AuditReference { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? Recommendation { get; set; }
    public Severity Rating { get; set; } = Severity.Medium;
    public string? ManagementResponse { get; set; }
    public Guid? ActionOwnerUserId { get; set; }
    public string ActionOwnerName { get; set; } = default!;
    public DateOnly DueDate { get; set; }
    public FindingStatus Status { get; set; } = FindingStatus.Open;
    public DateTime? ClosedAtUtc { get; set; }
}

public enum AssuranceLine
{
    Management,
    RiskAndCompliance,
    InternalAudit,
    ExternalAssurance
}

/// <summary>Combined assurance coverage map entry (FR-RSK-007).</summary>
public class AssuranceCoverage : AuditableEntity
{
    public string Area { get; set; } = default!;
    public Guid? RiskId { get; set; }
    public AssuranceLine Line { get; set; }
    public string Period { get; set; } = default!;
    public bool Covered { get; set; }
    public string? Provider { get; set; }
    public string? Rating { get; set; }
    public string? Comments { get; set; }
}
