namespace IFWEMS.Application.Compliance;

public record TransactionCheckRequest(
    string TransactionReference,
    Guid OrgUnitId,
    Guid? SupplierId,
    Guid? ContractId,
    decimal Amount,
    string? InvoiceReference,
    DateTime? PurchaseOrderDateUtc,
    string? CommodityCode);

public record TransactionCheckResult(
    int RiskScore,
    string RiskRating,
    IReadOnlyList<string> FailedRuleCodes,
    string RecommendedAction);

public record EmergencyOverrideRequest(
    string TransactionReference,
    Guid OrgUnitId,
    Guid? SupplierId,
    decimal Amount,
    string EmergencyCategory,
    string Reason,
    Guid EvidenceDocumentId,
    string? CommodityCode);
