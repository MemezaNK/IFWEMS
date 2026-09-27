using Platform.Core;
using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Contracts;

public enum ContractStatus
{
    Draft,
    PendingSignature,
    Active,
    Suspended,
    Expired,
    Closed,
    Terminated
}

public enum ContractSource
{
    Award,
    NonBid
}

public enum SignatureStatus
{
    Unsigned,
    PartiallySigned,
    Signed
}

/// <summary>
/// Contract (SRS §5.5, §24.2). Original value and original end date are immutable baselines; approved
/// variations/extensions adjust <see cref="ApprovedVariations"/> and <see cref="CurrentEndDate"/> only
/// (FR-CON-007/008, BR-005). Commitments may not exceed the revised ceiling (FR-CON-009).
/// </summary>
public class Contract : AuditableEntity
{
    public string ContractNumber { get; set; } = default!;
    public string Title { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid? ProcurementId { get; set; }
    public Guid? AwardId { get; set; }
    public Guid SupplierId { get; set; }
    public ContractSource Source { get; set; }
    public string? NonBidAuthority { get; set; }
    public decimal OriginalValue { get; set; }
    public decimal ApprovedVariations { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly OriginalEndDate { get; set; }
    public DateOnly CurrentEndDate { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public Guid? ContractManagerUserId { get; set; }
    public string? ContractManagerName { get; set; }
    public SignatureStatus SignatureStatus { get; set; } = SignatureStatus.Unsigned;
    public DateOnly? SignedDate { get; set; }
    public string? PoReference { get; set; }
    public decimal? PerformanceRating { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosureNotes { get; set; }
    public string? ClosureExceptionReference { get; set; }

    public decimal RevisedValue => OriginalValue + ApprovedVariations;

    public void ApplyApprovedVariation(decimal amount, DateOnly? revisedEndDate)
    {
        if (RevisedValue + amount < 0)
            throw new DomainException("A variation cannot reduce the contract value below zero.", "FR-CON-007");
        if (revisedEndDate is { } end && end < StartDate)
            throw new DomainException("Revised end date cannot precede the contract start date.", "FR-CON-008");
        ApprovedVariations += amount;
        if (revisedEndDate.HasValue) CurrentEndDate = revisedEndDate.Value;
    }

    /// <summary>FR-CON-009: can an additional commitment be absorbed within the approved ceiling?</summary>
    public bool CanCommit(decimal alreadyCommitted, decimal additional) => alreadyCommitted + additional <= RevisedValue;

    public void EnsureActive()
    {
        if (Status != ContractStatus.Active)
            throw new DomainException($"Contract {ContractNumber} is {Status}, not Active.", "BR-006");
    }
}

public enum ObligationType
{
    Deliverable,
    Milestone,
    Kpi,
    Sla,
    Reporting,
    Compliance
}

public enum ObligationStatus
{
    Open,
    Met,
    Missed,
    Waived
}

/// <summary>Contract obligation with an owner and due date (FR-CON-003).</summary>
public class ContractObligation : AuditableEntity
{
    public Guid ContractId { get; set; }
    public ObligationType Type { get; set; }
    public string Description { get; set; } = default!;
    public Guid? OwnerUserId { get; set; }
    public string OwnerName { get; set; } = default!;
    public DateOnly DueDate { get; set; }
    public string? EvidenceRequirement { get; set; }
    public string? KpiTarget { get; set; }
    public ObligationStatus Status { get; set; } = ObligationStatus.Open;
}

public enum AcceptanceStatus
{
    Pending,
    Submitted,
    Accepted,
    Rejected
}

/// <summary>Contract/project deliverable; acceptance gates payment certification (FR-CON-004, BR-006).</summary>
public class Deliverable : AuditableEntity
{
    public string Number { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? MilestoneId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal PayableAmount { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public bool EvidenceRequired { get; set; } = true;
    public AcceptanceStatus AcceptanceStatus { get; set; } = AcceptanceStatus.Pending;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public string? AcceptedBy { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>Payment milestone linked to a deliverable (FR-CON-004).</summary>
public class PaymentScheduleItem : AuditableEntity
{
    public Guid ContractId { get; set; }
    public Guid? DeliverableId { get; set; }
    public string Description { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateOnly PlannedDate { get; set; }
}

public enum VariationType
{
    Scope,
    Value,
    Time,
    Combined
}

/// <summary>Controlled contract variation / extension (FR-CON-007/008). Original and revised values stay visible.</summary>
public class ContractVariation : AuditableEntity
{
    public string Number { get; set; } = default!;
    public Guid ContractId { get; set; }
    public VariationType Type { get; set; }
    public bool IsExtension { get; set; }
    public string Description { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public decimal Amount { get; set; }
    public int Days { get; set; }
    public DateOnly? RevisedEndDate { get; set; }
    public decimal ValueBefore { get; set; }
    public decimal? ValueAfter { get; set; }
    public DateOnly EndDateBefore { get; set; }
    public DateOnly? EndDateAfter { get; set; }
    public ApprovalState Status { get; set; } = ApprovalState.Draft;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ChangeRequestId { get; set; }
}

/// <summary>Periodic supplier performance assessment against KPIs (FR-CON-010).</summary>
public class ContractPerformanceReview : AuditableEntity
{
    public Guid ContractId { get; set; }
    public Guid SupplierId { get; set; }
    public string Period { get; set; } = default!;
    public DateOnly ReviewDate { get; set; }
    public decimal QualityScore { get; set; }
    public decimal TimelinessScore { get; set; }
    public decimal ComplianceScore { get; set; }
    public decimal OverallScore { get; set; }
    public string Rating { get; set; } = default!;
    public string? Comments { get; set; }
    public string? ReviewedBy { get; set; }

    public static (decimal Overall, string Rating) Calculate(decimal quality, decimal timeliness, decimal compliance)
    {
        foreach (var s in new[] { quality, timeliness, compliance })
        {
            if (s is < 0 or > 5) throw new DomainException("Performance scores must be between 0 and 5.", "FR-CON-010");
        }
        var overall = Math.Round((quality + timeliness + compliance) / 3m, 2);
        var rating = overall switch
        {
            >= 4.5m => "Excellent",
            >= 3.5m => "Good",
            >= 2.5m => "Satisfactory",
            >= 1.5m => "Poor",
            _ => "Unacceptable"
        };
        return (overall, rating);
    }
}

public enum BreachStatus
{
    Open,
    NoticeIssued,
    Remedied,
    Escalated,
    Closed
}

/// <summary>Non-performance, notices and remedies (FR-CON-011).</summary>
public class ContractBreach : AuditableEntity
{
    public Guid ContractId { get; set; }
    public string Description { get; set; } = default!;
    public Severity Severity { get; set; } = Severity.Medium;
    public DateOnly IdentifiedOn { get; set; }
    public DateOnly? NoticeDate { get; set; }
    public string? NoticeReference { get; set; }
    public string? Remedy { get; set; }
    public DateOnly? RemedyDueDate { get; set; }
    public decimal? PenaltyAmount { get; set; }
    public BreachStatus Status { get; set; } = BreachStatus.Open;

    public bool IsOpen => Status is not (BreachStatus.Remedied or BreachStatus.Closed);
}
