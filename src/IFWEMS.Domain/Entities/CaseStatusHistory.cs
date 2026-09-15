namespace IFWEMS.Domain.Entities;

/// <summary>
/// Immutable record of every case status transition. Never updated or deleted. FR-024.
/// </summary>
public class CaseStatusHistory
{
    public long Id { get; set; }
    public Guid CaseId { get; set; }
    public Case Case { get; set; } = default!;
    public CaseStatus FromStatus { get; set; }
    public CaseStatus ToStatus { get; set; }
    public string? ChangedBy { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
