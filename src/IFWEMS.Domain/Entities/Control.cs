namespace IFWEMS.Domain.Entities;

/// <summary>
/// Control library entry that confirmed incidents must be linked to. FR-026.
/// </summary>
public class Control : Common.BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public ICollection<CorrectiveAction> CorrectiveActions { get; set; } = new List<CorrectiveAction>();
}

public enum CorrectiveActionStatus
{
    Planned,
    InProgress,
    ImplementedPendingVerification,
    Verified
}

/// <summary>
/// Corrective action linked to a control, requiring verified evidence before the case can be
/// closed. FR-026.
/// </summary>
public class CorrectiveAction : Common.BaseEntity
{
    public Guid CaseId { get; set; }
    public Case Case { get; set; } = default!;
    public Guid ControlId { get; set; }
    public Control Control { get; set; } = default!;
    public string Description { get; set; } = default!;
    public CorrectiveActionStatus Status { get; set; } = CorrectiveActionStatus.Planned;
    public Guid? EvidenceDocumentId { get; set; }
    public Document? EvidenceDocument { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
}
