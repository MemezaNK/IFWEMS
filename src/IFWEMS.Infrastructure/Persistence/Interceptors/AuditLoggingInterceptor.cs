using System.Text.Json;
using IFWEMS.Application.Common.Interfaces;
using IFWEMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IFWEMS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Captures every material create/update/delete on domain entities into the immutable
/// AuditLog table: user, session, action, entity, before/after values, timestamp,
/// IP address and user agent. FR-051.
/// </summary>
public class AuditLoggingInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserContext _currentUser;

    public AuditLoggingInterceptor(ICurrentUserContext currentUser)
    {
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AppendAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AppendAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AppendAuditEntries(DbContext? context)
    {
        if (context is null) return;

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0) return;

        foreach (var entry in entries)
        {
            var (before, after) = CaptureValues(entry);

            context.Set<AuditLog>().Add(new AuditLog
            {
                UserId = _currentUser.UserId,
                Username = _currentUser.Username,
                SessionId = _currentUser.SessionId,
                Action = entry.State.ToString(),
                EntityName = entry.Entity.GetType().Name,
                EntityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString(),
                BeforeValuesJson = before,
                AfterValuesJson = after,
                TimestampUtc = DateTime.UtcNow,
                IpAddress = _currentUser.IpAddress,
                UserAgent = _currentUser.UserAgent
            });
        }
    }

    private static (string? Before, string? After) CaptureValues(EntityEntry entry)
    {
        return entry.State switch
        {
            EntityState.Added => (null, Serialize(entry.CurrentValues)),
            EntityState.Deleted => (Serialize(entry.OriginalValues), null),
            EntityState.Modified => (Serialize(entry.OriginalValues), Serialize(entry.CurrentValues)),
            _ => (null, null)
        };
    }

    private static string Serialize(PropertyValues values)
    {
        var dict = values.Properties.ToDictionary(p => p.Name, p => values[p]);
        return JsonSerializer.Serialize(dict);
    }
}
