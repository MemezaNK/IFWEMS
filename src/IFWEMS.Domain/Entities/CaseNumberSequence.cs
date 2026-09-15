namespace IFWEMS.Domain.Entities;

/// <summary>
/// Backing store for atomic, non-reusable case number generation. FR-020.
/// One row per (Department, CaseType, FiscalYear) combination.
/// </summary>
public class CaseNumberSequence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DepartmentCode { get; set; } = default!;
    public string CaseTypeCode { get; set; } = default!;
    public int FiscalYear { get; set; }
    public int LastValue { get; set; }
}
