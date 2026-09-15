namespace IFWEMS.Domain.Entities;

/// <summary>
/// Supplier risk profile aggregation. FR-031.
/// </summary>
public class Supplier : Common.BaseEntity
{
    public string SupplierCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? RegistrationNumber { get; set; }
    public int DeviationCount { get; set; }
    public int CaseCount { get; set; }
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<Case> Cases { get; set; } = new List<Case>();
}
