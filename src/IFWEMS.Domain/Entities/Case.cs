namespace IFWEMS.Domain.Entities;

public enum CaseType
{
    IrregularExpenditure,   // IE
    FruitlessWasteful,      // FWE
    UnauthorisedExpenditure,// UE
    PotentialNonCompliance  // PNC
}

public enum CaseStatus
{
    Draft,
    UnderAssessment,
    UnderInvestigation,
    Determined,
    RecoveryInProgress,
    ConsequenceManagement,
    CorrectiveActionPending,
    Closed
}

/// <summary>
/// Core case record. FR-020 to FR-026.
/// </summary>
public class Case : Common.BaseEntity
{
    /// <summary>Unique, centrally issued, non-reusable case number: [DEPT]-[TYPE]-[FY]-[SEQUENCE]. FR-020.</summary>
    public string CaseNumber { get; set; } = default!;
    public CaseType CaseType { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Draft;
    public Guid OrgUnitId { get; set; }
    public OrgUnit OrgUnit { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public decimal? AmountInvolved { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
}
