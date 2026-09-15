using IFWEMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Infrastructure.Persistence;

public class IfwemsDbContext : DbContext
{
    public IfwemsDbContext(DbContextOptions<IfwemsDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ComplianceRule> ComplianceRules => Set<ComplianceRule>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IfwemsDbContext).Assembly);

        // Composite keys for join entities
        modelBuilder.Entity<RolePermission>().HasKey(rp => new { rp.RoleId, rp.PermissionId });
        modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<OrgUnit>()
            .HasOne(o => o.ParentOrgUnit)
            .WithMany(o => o.Children)
            .HasForeignKey(o => o.ParentOrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(r => r.Name).IsUnique();
        modelBuilder.Entity<Permission>().HasIndex(p => p.Code).IsUnique();
        modelBuilder.Entity<Case>().HasIndex(c => c.CaseNumber).IsUnique();
        modelBuilder.Entity<ComplianceRule>().HasIndex(r => new { r.Code, r.Version }).IsUnique();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetColumnType("decimal(18,2)");
                }
            }
        }
    }
}
