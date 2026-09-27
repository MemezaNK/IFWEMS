using Teta.Ippcms.Domain.Scm;

namespace Teta.Ippcms.Application.Sourcing;

public sealed record RequisitionDto(Guid Id, string Number, Guid ProjectId, string ProjectReference, Guid? PlanItemId, Guid? BudgetLineId,
    string Title, string? Description, decimal EstimatedValue, DateOnly? RequiredByDate, string? RecommendedMethod, string? SelectedMethod,
    string? MethodJustification, bool BudgetAvailable, string? BudgetCheckResult, Guid? ExceptionId, string Status, Guid? ProcurementId,
    string? CreatedBy, DateTime CreatedAtUtc, long Version);

public sealed record SaveRequisitionRequest(Guid ProjectId, Guid? PlanItemId, Guid? BudgetLineId, string Title, string? Description,
    decimal EstimatedValue, DateOnly? RequiredByDate, string? SelectedMethod, string? MethodJustification, Guid? ExceptionId);

public sealed record ProcurementListItemDto(Guid Id, string Number, string Title, Guid ProjectId, string ProjectReference, string Method,
    decimal EstimatedValue, string Status, DateTime? ClosingDateUtc, int BidCount, int DaysOpen, string? OrgUnit, DateOnly? PlannedAwardDate,
    DateOnly? ActualAwardDate);

public sealed record ProcurementQuery(string? Search, Guid? ProjectId, string? Status, string? Method, string? OrgUnit, int Page = 1, int PageSize = 50);

public sealed record SpecificationDto(Guid Id, int VersionNumber, string Title, string Content, string Status, DateTime? ApprovedAtUtc,
    string? ApprovedBy, string? CreatedBy, long Version);
public sealed record SaveSpecificationRequest(string Title, string Content);

public sealed record CommitteeMemberDto(Guid Id, Guid UserId, string Name, string Role, DateOnly AccessFrom, DateOnly AccessTo, bool HasAccessToday,
    bool Declared, bool? HasConflict);
public sealed record CommitteeDto(Guid Id, string Type, string Name, int Quorum, IReadOnlyList<CommitteeMemberDto> Members);
public sealed record SaveCommitteeRequest(CommitteeType Type, string Name, int Quorum);
public sealed record AddMemberRequest(Guid UserId, CommitteeRole Role, DateOnly AccessFrom, DateOnly AccessTo);
public sealed record MeetingDto(Guid Id, Guid CommitteeId, DateTime MeetingAtUtc, string Agenda, string? Minutes, int AttendeesCount, bool QuorumMet,
    string? Resolutions);
public sealed record SaveMeetingRequest(DateTime MeetingAtUtc, string Agenda, string? Minutes, int AttendeesCount, string? Resolutions);

public sealed record DeclarationDto(Guid Id, Guid UserId, string DeclarantName, bool HasConflict, string? ConflictDetails, bool ConfidentialityAccepted,
    DateTime DeclaredAtUtc);
public sealed record DeclareRequest(bool HasConflict, string? ConflictDetails, bool ConfidentialityAccepted);

public sealed record PublicationDto(Guid Id, string Channel, string Reference, DateOnly PublishedOn, DateTime ClosingDateUtc, string? Url,
    string? BriefingSession, Guid? EvidenceDocumentId);
public sealed record SavePublicationRequest(string Channel, string Reference, DateOnly PublishedOn, DateTime ClosingDateUtc, string? Url,
    string? BriefingSession, Guid? EvidenceDocumentId);

public sealed record BidDto(Guid Id, Guid SupplierId, string SupplierNumber, string SupplierName, int? BbbeeLevel, string BidReference,
    DateTime ReceivedAtUtc, bool IsLate, decimal? BidAmount, string Status, DateTime? OpenedAtUtc, string? OpenedBy, string? InvalidReason,
    bool? ComplianceMet, decimal? TechnicalScore, decimal? PricePoints, decimal? PreferencePoints, decimal? SpecificGoalsClaimed,
    decimal? TotalPoints, int? Rank);
public sealed record RegisterBidRequest(Guid SupplierId, string BidReference, decimal BidAmount, decimal? SpecificGoalsClaimed, string? SubmissionNotes);

public sealed record CriterionDto(Guid Id, string Stage, string Name, string? Description, decimal Weight, decimal MaxScore, bool IsMandatory, int SortOrder);
public sealed record SaveCriterionRequest(EvaluationStage Stage, string Name, string? Description, decimal Weight, decimal MaxScore, bool IsMandatory, int SortOrder);
public sealed record EvaluationSetupRequest(decimal? TechnicalThreshold, string? PriceSystemCode);

public sealed record ScoreDto(Guid Id, Guid BidId, Guid CriterionId, Guid EvaluatorUserId, string EvaluatorName, decimal? Score, bool? Passed,
    string Comment, string? EvidenceReference, DateTime ScoredAtUtc);
public sealed record SubmitScoreRequest(Guid BidId, Guid CriterionId, decimal? Score, bool? Passed, string Comment, string? EvidenceReference);

public sealed record EvaluationReportDto(Guid ProcurementId, string ProcurementNumber, decimal? TechnicalThreshold, string? PriceSystemCode,
    decimal? PricePointsMax, decimal? PreferencePointsMax, IReadOnlyList<CriterionDto> Criteria, IReadOnlyList<ScoreDto> Scores,
    IReadOnlyList<BidDto> Bids, string Method, string Notes);

public sealed record DueDiligenceDto(Guid Id, Guid BidId, Guid SupplierId, bool CsdVerified, bool TaxCompliant, bool NotRestricted, bool NotOnDefaultersList,
    bool ReferencesChecked, bool CapacityConfirmed, string Outcome, string? Notes, DateTime? CompletedAtUtc, string? CompletedBy);
public sealed record SaveDueDiligenceRequest(Guid BidId, bool CsdVerified, bool TaxCompliant, bool NotRestricted, bool NotOnDefaultersList,
    bool ReferencesChecked, bool CapacityConfirmed, string? Notes);

public sealed record AdjudicationDto(Guid Id, Guid RecommendedBidId, string RecommendedSupplier, decimal RecommendedAmount, Guid? DueDiligenceCheckId,
    string Recommendation, string Decision, string? Conditions, string? Reasons, DateTime? DecidedAtUtc, string? DecidedBy, Guid? WorkflowInstanceId);
public sealed record SubmitAdjudicationRequest(Guid RecommendedBidId, string Recommendation, string? Conditions);

public sealed record AwardDto(Guid Id, string Number, Guid ProcurementId, string ProcurementNumber, Guid BidId, Guid SupplierId, string SupplierName,
    Guid ProjectId, decimal Amount, DateOnly AwardDate, string Status, string? Conditions, bool ConditionsSatisfied, Guid? ContractId);

public sealed record CommunicationDto(Guid Id, Guid? BidId, Guid? SupplierId, string? SupplierName, string Type, DateOnly Date, string Subject,
    string Details, string? Outcome);
public sealed record SaveCommunicationRequest(Guid? BidId, Guid? SupplierId, CommunicationType Type, DateOnly Date, string Subject, string Details,
    string? Outcome);

public sealed record ExceptionDto(Guid Id, string Number, Guid? ProjectId, Guid? ProcurementId, Guid? RequisitionId, string Type, string Motivation,
    string Authority, decimal? Value, string Status, DateTime? DecidedAtUtc, string? DecidedBy, string? DecisionReason, string? CreatedBy, DateTime CreatedAtUtc);
public sealed record SaveExceptionRequest(Guid? ProjectId, Guid? ProcurementId, Guid? RequisitionId, ExceptionType Type, string Motivation,
    string Authority, decimal? Value);

public sealed record ProcurementDetailDto(ProcurementListItemDto Summary, Guid? RequisitionId, string? RequisitionNumber, decimal? TechnicalThreshold,
    string? PriceSystemCode, string? CancellationReason, Guid? ReAdvertisedFromId, Guid? ReAdvertisedAsId, string? MethodRuleInfo,
    bool CanAccessBids, bool IsEvaluator, bool HasDeclared, IReadOnlyList<SpecificationDto> Specifications, IReadOnlyList<CommitteeDto> Committees,
    IReadOnlyList<PublicationDto> Publications, IReadOnlyList<CriterionDto> Criteria, AdjudicationDto? Adjudication, AwardDto? Award,
    long Version);

public sealed record TransparencyRow(string TenderNumber, string Description, string Method, string? ClosingDate, int BidsReceived,
    string? SuccessfulBidder, string? SupplierRegistration, int? BbbeeLevel, decimal? AwardValue, string? AwardDate, string Status,
    string ProjectId, string? CancellationReason);

public sealed record ProcurementDashboardDto(int Total, decimal TotalEstimatedValue, IReadOnlyList<StatusBucket> ByStatus,
    IReadOnlyList<AgeBucket> Ageing, IReadOnlyList<MethodBucket> ByMethod, int Delayed, int ApprovedExceptions, int PendingExceptions,
    int LateBids, IReadOnlyList<ProcurementListItemDto> Oldest, IReadOnlyList<ProcurementListItemDto> DelayedItems);
public sealed record StatusBucket(string Status, int Count, decimal Value);
public sealed record AgeBucket(string Bucket, int Count);
public sealed record MethodBucket(string Method, int Count, decimal Value);
