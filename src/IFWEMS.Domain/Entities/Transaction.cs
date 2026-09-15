namespace IFWEMS.Domain.Entities;

public enum ComplianceAction
{
    Pass,
    Review,
    Block
}

/// <summary>
/// A screened financial transaction and its compliance-check outcome. FR-010.
/// </summary>
public class Transaction : Common.BaseEntity
{
    public string TransactionReference { get; set; } = default!;
    public Guid OrgUnitId { get; set; }
    public OrgUnit OrgUnit { get; set; } = default!;
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public decimal Amount { get; set; }
    public int RiskScore { get; set; }
    public string RiskRating { get; set; } = "GREEN"; // GREEN / AMBER / RED
    public ComplianceAction RecommendedAction { get; set; } = ComplianceAction.Pass;
    public string? FailedRuleCodes { get; set; } // CSV of ComplianceRule.Code
    public bool IsEmergencyOverride { get; set; }
    public string? OverrideReason { get; set; }
    public string? OverrideApprovedBy { get; set; }
}
