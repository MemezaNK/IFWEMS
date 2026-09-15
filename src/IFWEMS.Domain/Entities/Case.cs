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
    public decimal RecoverableAmount { get; set; }
    public decimal RecoveredAmount { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public ICollection<CaseAssessment> Assessments { get; set; } = new List<CaseAssessment>();
    public ICollection<Investigation> Investigations { get; set; } = new List<Investigation>();
    public ICollection<Recovery> Recoveries { get; set; } = new List<Recovery>();
    public ICollection<CorrectiveAction> CorrectiveActions { get; set; } = new List<CorrectiveAction>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
