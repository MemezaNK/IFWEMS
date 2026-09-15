namespace IFWEMS.Domain.Entities;

public enum RecoveryStatus
{
    Captured,
    PendingWriteOffApproval,
    Recovered,
    WrittenOff
}

/// <summary>
/// An individually tracked recovery transaction reconciled against the case's recoverable
/// balance. FR-025.
/// </summary>
public class Recovery : Common.BaseEntity
{
    public Guid CaseId { get; set; }
    public Case Case { get; set; } = default!;
    public decimal Amount { get; set; }
    public RecoveryStatus Status { get; set; } = RecoveryStatus.Captured;
    public string? Reference { get; set; }
    public Guid CapturedByUserId { get; set; }
    public User CapturedByUser { get; set; } = default!;
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? WriteOffReason { get; set; }
}
