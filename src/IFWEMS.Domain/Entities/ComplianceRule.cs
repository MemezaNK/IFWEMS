namespace IFWEMS.Domain.Entities;

/// <summary>
/// Versioned, effective-dated, configurable compliance rule. FR-011, FR-012.
/// </summary>
public class ComplianceRule : Common.BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; }
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    /// <summary>JSON-encoded rule parameters/thresholds (e.g. ceiling amount, window days).</summary>
    public string ParametersJson { get; set; } = "{}";
    public bool IsApproved { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
}
