namespace IFWEMS.Domain.Entities;

/// <summary>
/// Hierarchical organisational unit (Department -> District -> Facility -> Cost Centre). FR-004.
/// </summary>
public class OrgUnit : Common.BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Level { get; set; } = default!; // Department, District, Facility, CostCentre
    public Guid? ParentOrgUnitId { get; set; }
    public OrgUnit? ParentOrgUnit { get; set; }
    public ICollection<OrgUnit> Children { get; set; } = new List<OrgUnit>();
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
