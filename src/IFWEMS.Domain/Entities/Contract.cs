namespace IFWEMS.Domain.Entities;

/// <summary>
/// Contract tracked for value, variations, utilisation, and expiry alerts. FR-030.
/// </summary>
public class Contract : Common.BaseEntity
{
    public string ContractNumber { get; set; } = default!;
    public string Title { get; set; } = default!;
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;
    public decimal OriginalValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal UtilisedValue { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime ExpiryDateUtc { get; set; }
    public ICollection<Case> Cases { get; set; } = new List<Case>();
}
