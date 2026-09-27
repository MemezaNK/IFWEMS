using Microsoft.EntityFrameworkCore;

namespace Platform.Security.Identity;

/// <summary>
/// A user in the platform's shared identity store (<c>dbo.Users</c>). The table is created and
/// migrated by IFWEMS; other applications (e.g. TETA-IPPCMS) map the same table so one set of
/// credentials works across applications, while each application keeps its own role/permission
/// assignments. Column names and types mirror IFWEMS's <c>User</c> entity exactly.
/// </summary>
public class PlatformUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public bool MfaEnabled { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedOutUntilUtc { get; set; }
    public Guid? OrgUnitId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsLockedOut(DateTime utcNow) => LockedOutUntilUtc is { } until && until > utcNow;
}

public static class PlatformIdentityModelBuilderExtensions
{
    /// <summary>
    /// Maps <see cref="PlatformUser"/> onto the shared <c>dbo.Users</c> table and excludes it from
    /// the calling context's migrations (IFWEMS owns that table's schema).
    /// </summary>
    public static ModelBuilder MapSharedPlatformUsers(this ModelBuilder modelBuilder, string schema = "dbo")
    {
        modelBuilder.Entity<PlatformUser>(b =>
        {
            b.ToTable("Users", schema, t => t.ExcludeFromMigrations());
            b.HasKey(u => u.Id);
            b.Property(u => u.Username).HasMaxLength(450).IsRequired();
            b.Property(u => u.Email).HasMaxLength(450).IsRequired();
            b.Property(u => u.DisplayName).IsRequired();
            b.Property(u => u.PasswordHash).IsRequired();
            b.HasIndex(u => u.Username).IsUnique();
            b.HasIndex(u => u.Email).IsUnique();
        });
        return modelBuilder;
    }
}
