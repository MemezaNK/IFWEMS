namespace IFWEMS.Domain.Entities;

/// <summary>
/// Configurable, effective-dated/versioned SLA for a case type/stage combination. FR-040.
/// </summary>
public class SlaPolicy : Common.BaseEntity
{
    public CaseType CaseType { get; set; }
    public CaseStatus Status { get; set; }
    public int MaxDurationHours { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
}
