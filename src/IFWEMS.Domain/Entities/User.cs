namespace IFWEMS.Domain.Entities;

/// <summary>
/// Application user. Native user management independent of external directory. FR-001.
/// </summary>
public class User : Common.BaseEntity
{
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public bool MfaEnabled { get; set; }

    /// <summary>Consecutive failed login attempts since the last successful login or unlock.</summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>When set and in the future, the account is locked out until this time (NFR 4.2 #13).</summary>
    public DateTime? LockedOutUntilUtc { get; set; }
    public Guid? OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
