namespace IFWEMS.Domain.Entities;

/// <summary>
/// Native RBAC role. FR-002.
/// </summary>
public class Role : Common.BaseEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
