namespace IFWEMS.Domain.Entities;

public enum InvestigationStatus
{
    InProgress,
    PendingApproval,
    Approved,
    Rejected
}

/// <summary>
/// Investigation performed against a case, subject to maker (Investigator) / checker
/// (ApprovingOfficial) segregation of duties. FR-003, FR-023.
/// </summary>
public class Investigation : Common.BaseEntity
{
    public Guid CaseId { get; set; }
    public Case Case { get; set; } = default!;
    public Guid InvestigatorUserId { get; set; }
    public User InvestigatorUser { get; set; } = default!;
    public InvestigationStatus Status { get; set; } = InvestigationStatus.InProgress;
    public string? Findings { get; set; }
    public string? Recommendation { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
}
