namespace IFWEMS.Domain.Entities;

/// <summary>
/// Editable, self-contained system-wide configuration parameter (key/value). PLAT-04.
/// </summary>
public class SystemSetting : Common.BaseEntity
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string? Description { get; set; }
}
