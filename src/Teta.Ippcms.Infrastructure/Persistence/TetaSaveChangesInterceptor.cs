using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Platform.Audit;
using Platform.Core;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Budget;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Execution;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Monitoring;
using Teta.Ippcms.Domain.Projects;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Strategy;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.Infrastructure.Persistence;

/// <summary>
/// Runs on every SaveChanges:
///  1. stamps created/updated metadata and increments the concurrency version (SRS §7, NFR-004);
///  2. refuses updates/deletes of append-only history and audit records (SEC-010, BR-005);
///  3. writes a sealed audit record for every material change with old/new values (SRS §14, FR-ADM-008).
/// </summary>
public sealed class TetaSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<Type> AppendOnly = new()
    {
        typeof(AuditLog), typeof(ProjectStatusHistory), typeof(StrategicPlanVersion), typeof(ScheduleBaseline),
        typeof(TransactionAction), typeof(BudgetRevision), typeof(ProgressUpdate), typeof(CostForecast),
        typeof(ControlAssessment), typeof(BeneficiaryStatusHistory), typeof(EscalationEvent)
    };

    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly AuditSealer _sealer;

    public TetaSaveChangesInterceptor(ICurrentUser user, IClock clock, AuditSealer sealer)
    {
        _user = user;
        _clock = clock;
        _sealer = sealer;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Process(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Process(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Process(DbContext? context)
    {
        if (context is null) return;
        var now = _clock.UtcNow;
        var username = _user.Username ?? "system";

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted && AppendOnly.Contains(entry.Entity.GetType()))
            {
                throw new DomainException(
                    $"{entry.Entity.GetType().Name} records are append-only and cannot be modified or deleted.", "SEC-010");
            }

            if (entry.Entity is AuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAtUtc = now;
                    auditable.CreatedBy ??= username;
                    auditable.CreatedByUserId ??= _user.UserId;
                    auditable.Version = 1;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAtUtc = now;
                    auditable.UpdatedBy = username;
                    auditable.Version++;
                }
            }
        }

        var changes = AuditCapture.Capture(context.ChangeTracker);
        foreach (var change in changes)
        {
            var log = new AuditLog
            {
                OccurredAtUtc = now,
                UserId = _user.UserId,
                Username = username,
                CorrelationId = _user.CorrelationId ?? Guid.NewGuid().ToString("N"),
                SessionId = _user.SessionId,
                Module = ModuleFor(context, change.EntityType),
                EntityType = change.EntityType,
                EntityId = change.EntityId,
                Action = change.Action,
                OldValues = change.OldValuesJson,
                NewValues = change.NewValuesJson,
                SourceIp = _user.IpAddress,
                UserAgent = _user.UserAgent
            };
            log.Seal = AuditLogSealing.Seal(_sealer, log);
            context.Set<AuditLog>().Add(log);
        }
    }

    private static string ModuleFor(DbContext context, string entityTypeName)
    {
        var clr = context.Model.GetEntityTypes().FirstOrDefault(t => t.ClrType.Name == entityTypeName)?.ClrType;
        return clr is null ? "General" : AuditLogSealing.ModuleOf(clr);
    }
}
