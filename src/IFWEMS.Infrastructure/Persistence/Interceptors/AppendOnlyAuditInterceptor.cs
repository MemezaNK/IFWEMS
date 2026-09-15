using IFWEMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IFWEMS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Enforces FR-024/FR-051: audit log records (and case history, by extension) can never be
/// modified or deleted through the application. Throws if such an operation is attempted.
/// </summary>
public class AppendOnlyAuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Guard(DbContext? context)
    {
        if (context is null) return;

        var auditViolations = context.ChangeTracker.Entries<AuditLog>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);
        var caseHistoryViolations = context.ChangeTracker.Entries<CaseStatusHistory>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (auditViolations)
        {
            throw new InvalidOperationException(
                "Audit log records are append-only and cannot be modified or deleted (FR-024/FR-051).");
        }

        if (caseHistoryViolations)
        {
            throw new InvalidOperationException(
                "Case status history records are append-only and cannot be modified or deleted (FR-024).");
        }
    }
}
