using Platform.Core;
using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Scm;

public enum RequisitionStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    Converted,
    Cancelled
}

/// <summary>Electronic procurement requisition linked to an approved project and budget (FR-SCM-001, BR-002, FR-BUD-005).</summary>
public class Requisition : AuditableEntity
{
    public string Number { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid? PlanItemId { get; set; }
    public Guid? BudgetLineId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public decimal EstimatedValue { get; set; }
    public DateOnly? RequiredByDate { get; set; }
    public string? RecommendedMethod { get; set; }
    public string? SelectedMethod { get; set; }
    public string? MethodJustification { get; set; }
    public Guid? MethodRuleId { get; set; }
    public int? MethodRuleVersion { get; set; }
    public bool BudgetAvailable { get; set; }
    public string? BudgetCheckResult { get; set; }
    public Guid? ExceptionId { get; set; }
    public RequisitionStatus Status { get; set; } = RequisitionStatus.Draft;
    public Guid? ProcurementId { get; set; }

    public IReadOnlyList<string> MissingMandatoryFields()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(Title)) missing.Add(nameof(Title));
        if (string.IsNullOrWhiteSpace(Description)) missing.Add(nameof(Description));
        if (EstimatedValue <= 0) missing.Add(nameof(EstimatedValue));
        if (RequiredByDate is null) missing.Add(nameof(RequiredByDate));
        if (BudgetLineId is null) missing.Add(nameof(BudgetLineId));
        return missing;
    }
}

public enum ProcurementStatus
{
    Planning,
    SpecificationApproved,
    Published,
    BidsClosed,
    Evaluation,
    Adjudication,
    Awarded,
    Cancelled
}

/// <summary>A sourcing process from specification through award (SRS §5.4).</summary>
public class Procurement : AuditableEntity
{
    public string Number { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid? RequisitionId { get; set; }
    public string Title { get; set; } = default!;
    public string Method { get; set; } = default!;
    public Guid? MethodRuleId { get; set; }
    public int? MethodRuleVersion { get; set; }
    public decimal EstimatedValue { get; set; }
    public ProcurementStatus Status { get; set; } = ProcurementStatus.Planning;
    public DateTime? ClosingDateUtc { get; set; }
    public DateTime? BidOpeningAtUtc { get; set; }
    public decimal? TechnicalThreshold { get; set; }
    public string? PriceSystemCode { get; set; }
    public Guid? PriceSystemId { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public Guid? ReAdvertisedFromId { get; set; }
    public Guid? ReAdvertisedAsId { get; set; }
    public string? OrgUnit { get; set; }
    public DateOnly? PlannedAwardDate { get; set; }
    public DateOnly? ActualAwardDate { get; set; }

    public bool IsTerminal => Status is ProcurementStatus.Awarded or ProcurementStatus.Cancelled;

    public void EnsureNotTerminal()
    {
        if (IsTerminal) throw new DomainException($"Procurement {Number} is {Status}; the record is closed for changes.", "FR-SCM-014");
    }
}

public enum SpecificationStatus
{
    Draft,
    UnderReview,
    Approved,
    Superseded
}

/// <summary>Versioned specification / terms of reference; approved version is locked (FR-SCM-002).</summary>
public class Specification : AuditableEntity
{
    public Guid ProcurementId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string Title { get; set; } = default!;
    public string Content { get; set; } = default!;
    public SpecificationStatus Status { get; set; } = SpecificationStatus.Draft;
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }

    public void EnsureEditable()
    {
        if (Status is SpecificationStatus.Approved or SpecificationStatus.Superseded)
            throw new DomainException("An approved specification version is locked. Create a new version.", "FR-SCM-002");
    }
}

public enum CommitteeType
{
    BidSpecification,
    BidEvaluation,
    BidAdjudication
}

/// <summary>Bid committee with members, roles and quorum (FR-SCM-003).</summary>
public class Committee : AuditableEntity
{
    public Guid? ProcurementId { get; set; }
    public CommitteeType Type { get; set; }
    public string Name { get; set; } = default!;
    public int Quorum { get; set; } = 3;
    public ICollection<CommitteeMember> Members { get; set; } = new List<CommitteeMember>();
}

public enum CommitteeRole
{
    Chairperson,
    Member,
    Secretariat,
    Observer
}

/// <summary>Time-bound committee membership granting access to protected procurement documents.</summary>
public class CommitteeMember : AuditableEntity
{
    public Guid CommitteeId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    public CommitteeRole Role { get; set; } = CommitteeRole.Member;
    public DateOnly AccessFrom { get; set; }
    public DateOnly AccessTo { get; set; }

    public bool HasAccessOn(DateOnly date) => AccessFrom <= date && AccessTo >= date;
}

public class CommitteeMeeting : AuditableEntity
{
    public Guid CommitteeId { get; set; }
    public DateTime MeetingAtUtc { get; set; }
    public string Agenda { get; set; } = default!;
    public string? Minutes { get; set; }
    public int AttendeesCount { get; set; }
    public bool QuorumMet { get; set; }
    public string? Resolutions { get; set; }
}

/// <summary>Conflict-of-interest / confidentiality declaration required before bid access (FR-SCM-004, BR-003).</summary>
public class Declaration : AuditableEntity
{
    public Guid ProcurementId { get; set; }
    public Guid UserId { get; set; }
    public string DeclarantName { get; set; } = default!;
    public bool HasConflict { get; set; }
    public string? ConflictDetails { get; set; }
    public bool ConfidentialityAccepted { get; set; }
    public DateTime DeclaredAtUtc { get; set; }

    /// <summary>A declarant with a conflict is recused and does not get bid access.</summary>
    public bool GrantsAccess => ConfidentialityAccepted && !HasConflict;
}

/// <summary>Advertisement/publication record with closing date and evidence (FR-SCM-005).</summary>
public class Publication : AuditableEntity
{
    public Guid ProcurementId { get; set; }
    public string Channel { get; set; } = default!;
    public string Reference { get; set; } = default!;
    public DateOnly PublishedOn { get; set; }
    public DateTime ClosingDateUtc { get; set; }
    public string? Url { get; set; }
    public string? BriefingSession { get; set; }
    public Guid? EvidenceDocumentId { get; set; }
}

public enum BidStatus
{
    Received,
    Late,
    Opened,
    Invalid,
    NonCompliant,
    Compliant,
    NonResponsive,
    Responsive,
    Recommended,
    Awarded,
    Unsuccessful,
    Withdrawn
}

/// <summary>Bid submission with a server-side receipt timestamp and controlled opening (FR-SCM-006).</summary>
public class Bid : AuditableEntity
{
    public Guid ProcurementId { get; set; }
    public Guid SupplierId { get; set; }
    public string BidReference { get; set; } = default!;
    public DateTime ReceivedAtUtc { get; set; }
    public bool IsLate { get; set; }
    public decimal BidAmount { get; set; }
    public BidStatus Status { get; set; } = BidStatus.Received;
    public DateTime? OpenedAtUtc { get; set; }
    public string? OpenedBy { get; set; }
    public string? InvalidReason { get; set; }
    public bool? ComplianceMet { get; set; }
    public decimal? TechnicalScore { get; set; }
    public decimal? PricePoints { get; set; }
    public decimal? PreferencePoints { get; set; }
    public decimal? SpecificGoalsClaimed { get; set; }
    public decimal? TotalPoints { get; set; }
    public int? Rank { get; set; }
    public string? SubmissionNotes { get; set; }
}

public enum EvaluationStage
{
    Compliance,
    Technical
}

/// <summary>Configurable evaluation criterion: mandatory compliance item or weighted functionality criterion (FR-SCM-007/008).</summary>
public class EvaluationCriterion : AuditableEntity
{
    public Guid ProcurementId { get; set; }
    public EvaluationStage Stage { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Weight { get; set; }
    public decimal MaxScore { get; set; } = 5;
    public bool IsMandatory { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>An individual evaluator's result for one bid and criterion (spec entity "Evaluation").</summary>
public class EvaluationScore : AuditableEntity
{
    public Guid BidId { get; set; }
    public Guid CriterionId { get; set; }
    public Guid EvaluatorUserId { get; set; }
    public string EvaluatorName { get; set; } = default!;
    public decimal? Score { get; set; }
    public bool? Passed { get; set; }
    public string Comment { get; set; } = default!;
    public string? EvidenceReference { get; set; }
    public DateTime ScoredAtUtc { get; set; }
}

/// <summary>
/// Effective-dated price/preference evaluation system (FR-SCM-009, SRS §28). Points and formula are
/// configuration, not code, so policy changes don't require deployment.
/// </summary>
public class PricePreferenceSystem : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal PricePoints { get; set; }
    public decimal PreferencePoints { get; set; }
    public decimal? ApplicableFromValue { get; set; }
    public decimal? ApplicableToValue { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public ConfigStatus Status { get; set; } = ConfigStatus.Draft;
    public string? PolicyReference { get; set; }

    public bool IsEffectiveOn(DateOnly date) =>
        Status == ConfigStatus.Approved && EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo >= date);
}

public enum DueDiligenceOutcome
{
    Pending,
    Passed,
    Failed
}

/// <summary>Supplier verification and due diligence outcome (FR-SCM-010).</summary>
public class DueDiligenceCheck : AuditableEntity
{
    public Guid ProcurementId { get; set; }
    public Guid BidId { get; set; }
    public Guid SupplierId { get; set; }
    public bool CsdVerified { get; set; }
    public bool TaxCompliant { get; set; }
    public bool NotRestricted { get; set; }
    public bool NotOnDefaultersList { get; set; }
    public bool ReferencesChecked { get; set; }
    public bool CapacityConfirmed { get; set; }
    public DueDiligenceOutcome Outcome { get; set; } = DueDiligenceOutcome.Pending;
    public string? Notes { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? CompletedBy { get; set; }

    public bool AllChecksPassed => CsdVerified && TaxCompliant && NotRestricted && NotOnDefaultersList && ReferencesChecked && CapacityConfirmed;
}

public enum AdjudicationDecision
{
    Pending,
    Approved,
    Rejected,
    ReferredBack
}

/// <summary>Adjudication recommendation and electronically approved decision (FR-SCM-011).</summary>
public class Adjudication : AuditableEntity
{
    public Guid ProcurementId { get; set; }
    public Guid RecommendedBidId { get; set; }
    public Guid? DueDiligenceCheckId { get; set; }
    public string Recommendation { get; set; } = default!;
    public AdjudicationDecision Decision { get; set; } = AdjudicationDecision.Pending;
    public string? Conditions { get; set; }
    public string? Reasons { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
}

public enum AwardStatus
{
    PendingConditions,
    Final,
    Cancelled
}

/// <summary>Award record referencing the procurement and successful bidder (FR-SCM-012, BR-004).</summary>
public class Award : AuditableEntity
{
    public string Number { get; set; } = default!;
    public Guid ProcurementId { get; set; }
    public Guid BidId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid ProjectId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly AwardDate { get; set; }
    public AwardStatus Status { get; set; } = AwardStatus.PendingConditions;
    public string? Conditions { get; set; }
    public bool ConditionsSatisfied { get; set; }
    public Guid? ContractId { get; set; }

    /// <summary>BR-004: an award only creates an executable contract once final with conditions satisfied.</summary>
    public bool CanCreateContract => Status == AwardStatus.Final && ConditionsSatisfied && ContractId is null;
}

public enum CommunicationType
{
    AwardNotice,
    RegretLetter,
    Debriefing,
    Objection,
    ObjectionOutcome,
    Clarification
}

/// <summary>Unsuccessful-bidder notifications, debriefs and objections (FR-SCM-013).</summary>
public class BidderCommunication : AuditableEntity
{
    public Guid ProcurementId { get; set; }
    public Guid? BidId { get; set; }
    public Guid? SupplierId { get; set; }
    public CommunicationType Type { get; set; }
    public DateOnly Date { get; set; }
    public string Subject { get; set; } = default!;
    public string Details { get; set; } = default!;
    public string? Outcome { get; set; }
}

public enum ExceptionType
{
    Deviation,
    Emergency,
    SingleSource,
    BudgetException,
    Other
}

/// <summary>Approved deviation/exception with motivation and authority; never bypasses the audit trail (FR-SCM-015, BR-011).</summary>
public class ProcurementException : AuditableEntity
{
    public string Number { get; set; } = default!;
    public Guid? ProjectId { get; set; }
    public Guid? ProcurementId { get; set; }
    public Guid? RequisitionId { get; set; }
    public ExceptionType Type { get; set; }
    public string Motivation { get; set; } = default!;
    public string Authority { get; set; } = default!;
    public decimal? Value { get; set; }
    public ApprovalState Status { get; set; } = ApprovalState.Draft;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
    public string? DecisionReason { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
}
