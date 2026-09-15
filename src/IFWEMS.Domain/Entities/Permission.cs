namespace IFWEMS.Domain.Entities;

/// <summary>
/// A granular permission that can be assigned to roles. FR-002.
/// </summary>
public class Permission : Common.BaseEntity
{
    public string Code { get; set; } = default!; // e.g. "cases.create", "cases.approve"
    public string Description { get; set; } = default!;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}

public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;
    /// <summary>Optional scoping of the role assignment to a specific org unit (e.g. facility-level approver).</summary>
    public Guid? OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
}
