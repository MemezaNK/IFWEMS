using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Engines;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.Application.Workflow;

public sealed record StartWorkflowRequest(
    string DefinitionCode,
    string EntityType,
    Guid EntityId,
    string EntityReference,
    string Title,
    decimal? TransactionValue,
    Guid? ProjectId,
    string? Link = null);

public sealed record StepSnapshot(int StepOrder, string Code, string Name, string RequiredRole, string? AuthorityType, int SlaHours, string? EscalationRole);

public sealed record WorkflowTaskDto(
    Guid Id, int StepOrder, string StepCode, string StepName, string AssignedRole, string? AuthorityType,
    DateTime CreatedAtUtc, DateTime DueAtUtc, string Decision, string? DecidedBy, string? OnBehalfOf, string? Comment,
    DateTime? DecidedAtUtc, int EscalationLevel, string? EscalatedToRole, bool IsOverdue);

public sealed record WorkflowInstanceDto(
    Guid Id, string DefinitionCode, int DefinitionVersion, string EntityType, Guid EntityId, string? EntityReference,
    string? Title, decimal? TransactionValue, string State, int CurrentStepOrder, DateTime StartedAtUtc, string? StartedBy,
    DateTime? CompletedAtUtc, IReadOnlyList<StepSnapshot> Steps, IReadOnlyList<WorkflowTaskDto> Tasks);

public sealed record InboxItemDto(
    Guid TaskId, Guid InstanceId, string DefinitionCode, string EntityType, Guid EntityId, string? EntityReference,
    string? Title, decimal? TransactionValue, string StepName, string AssignedRole, DateTime DueAtUtc, bool IsOverdue,
    int EscalationLevel, string? OnBehalfOf, string? StartedBy, DateTime StartedAtUtc, Guid? ProjectId = null);

public sealed record DecisionResult(Guid InstanceId, string State, string Message, bool Escalated);

/// <summary>Applies the business effect of a finished workflow (e.g. business case approved → Project ID).</summary>
public interface IWorkflowCompletionHandler
{
    string EntityType { get; }
    Task OnCompletedAsync(WorkflowInstance instance, WorkflowState outcome, string? comment, CancellationToken cancellationToken);
}

public interface IWorkflowService
{
    Task<WorkflowInstance> StartAsync(StartWorkflowRequest request, CancellationToken cancellationToken = default);
    Task<DecisionResult> DecideAsync(Guid taskId, TaskDecision decision, string? comment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InboxItemDto>> GetInboxAsync(CancellationToken cancellationToken = default);
    Task<WorkflowInstanceDto> GetAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowInstanceDto>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
    Task<int> EscalateOverdueAsync(CancellationToken cancellationToken = default);
    Task CancelForEntityAsync(string entityType, Guid entityId, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configurable approval workflow engine (FR-ADM-001/002/003/005, SRS §27): versioned definitions,
/// value-based step routing, role-based assignment, acting/substitute approvers, delegation limits
/// (BR-007), segregation of duties (BR-008), SLA timers on the business calendar and escalation.
/// </summary>
public sealed class WorkflowService : IWorkflowService
{
    private static readonly JsonSerializerOptions Json = new();

    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IUserRoles _roles;
    private readonly IDelegationService _delegations;
    private readonly ISodService _sod;
    private readonly ITransactionLedger _ledger;
    private readonly IAuditWriter _audit;
    private readonly INotifier _notifier;
    private readonly ISettings _settings;
    private readonly IAccessScope _scope;
    private readonly IEnumerable<IWorkflowCompletionHandler> _handlers;

    public WorkflowService(ITetaDbContext db, ICurrentUser user, IClock clock, IUserRoles roles, IDelegationService delegations,
        ISodService sod, ITransactionLedger ledger, IAuditWriter audit, INotifier notifier, ISettings settings, IAccessScope scope,
        IEnumerable<IWorkflowCompletionHandler> handlers)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _roles = roles;
        _delegations = delegations;
        _sod = sod;
        _ledger = ledger;
        _audit = audit;
        _notifier = notifier;
        _settings = settings;
        _scope = scope;
        _handlers = handlers;
    }

    public async Task<WorkflowInstance> StartAsync(StartWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var alreadyRunning = await _db.WorkflowInstances.AnyAsync(i => i.EntityType == request.EntityType
            && i.EntityId == request.EntityId && i.State == WorkflowState.InProgress, cancellationToken);
        if (alreadyRunning) throw new ConflictException($"{request.EntityReference} already has an approval in progress.", "FR-ADM-001");

        var definition = await _db.WorkflowDefinitions.AsNoTracking()
            .Include(d => d.Steps)
            .Where(d => d.Code == request.DefinitionCode && d.Status == ConfigStatus.Approved)
            .OrderByDescending(d => d.DefinitionVersion)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException($"No approved workflow definition '{request.DefinitionCode}' is configured.", "FR-ADM-001");

        var value = request.TransactionValue ?? 0m;
        var steps = definition.Steps
            .Where(s => (s.MinimumValue is null || value >= s.MinimumValue) && (s.MaximumValue is null || value <= s.MaximumValue))
            .OrderBy(s => s.StepOrder)
            .Select(s => new StepSnapshot(s.StepOrder, s.Code, s.Name, s.RequiredRole, s.AuthorityType, s.SlaHours, s.EscalationRole))
            .ToList();

        var instance = new WorkflowInstance
        {
            DefinitionId = definition.Id,
            DefinitionCode = definition.Code,
            DefinitionVersion = definition.DefinitionVersion,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            EntityReference = request.EntityReference,
            Title = request.Title,
            TransactionValue = request.TransactionValue,
            ProjectId = request.ProjectId,
            StartedAtUtc = _clock.UtcNow,
            StartedByUserId = _user.UserId,
            StartedBy = _user.DisplayName ?? _user.Username,
            StepsSnapshotJson = JsonSerializer.Serialize(steps, Json)
        };
        _db.WorkflowInstances.Add(instance);
        _ledger.Record(request.EntityType, request.EntityId, "Submit", request.ProjectId);
        _audit.Write("Workflow", request.EntityType, request.EntityId.ToString(), "WorkflowStarted",
            new { request.DefinitionCode, definition.DefinitionVersion, request.TransactionValue, Steps = steps.Select(s => s.Code) });

        if (steps.Count == 0)
        {
            // Nothing to approve at this value: complete immediately so the business effect applies.
            instance.State = WorkflowState.Approved;
            instance.CompletedAtUtc = _clock.UtcNow;
            await CompleteAsync(instance, WorkflowState.Approved, "No approval steps apply at this value.", cancellationToken);
            return instance;
        }

        await CreateTaskAsync(instance, steps[0], request.Link, cancellationToken);
        return instance;
    }

    public async Task<DecisionResult> DecideAsync(Guid taskId, TaskDecision decision, string? comment, CancellationToken cancellationToken = default)
    {
        if (decision is not (TaskDecision.Approved or TaskDecision.Rejected or TaskDecision.Returned))
            throw new ValidationException("decision", "Decision must be Approved, Rejected or Returned.");
        if (decision != TaskDecision.Approved && string.IsNullOrWhiteSpace(comment))
            throw new ValidationException("comment", "A reason is required when rejecting or returning (BR-011).");
        var userId = _user.UserId ?? throw new ForbiddenException();

        var task = await _db.WorkflowTasks.SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken)
                   ?? throw new NotFoundException("Approval task", taskId);
        var instance = await _db.WorkflowInstances.Include(i => i.Tasks).SingleAsync(i => i.Id == task.InstanceId, cancellationToken);

        if (task.Decision != TaskDecision.Pending || instance.State != WorkflowState.InProgress)
            throw new ConflictException("This approval task has already been decided.");

        // Who is acting, and in what capacity (own role, or as substitute for a principal)?
        var (actingForUserId, actingForName, actingRoles) = await ResolveActingCapacityAsync(userId, task, cancellationToken);

        // SoD: initiator may not approve their own transaction; one person may not approve two steps.
        if (instance.StartedByUserId == userId || instance.StartedByUserId == actingForUserId)
            throw new DomainException("Segregation of duties: you cannot approve a transaction you submitted.", "BR-008");
        if (instance.Tasks.Any(t => t.Id != task.Id && t.Decision == TaskDecision.Approved && (t.DecidedByUserId == userId || t.OnBehalfOfUserId == userId)))
            throw new DomainException("Segregation of duties: you already approved an earlier step of this transaction.", "BR-008");

        try
        {
            await _sod.EnsureAllowedAsync(instance.EntityType, instance.EntityId, task.StepCode, userId, cancellationToken);
        }
        catch (SodEscalationRequired sod)
        {
            return await EscalateForSodAsync(instance, task, sod, cancellationToken);
        }

        if (decision == TaskDecision.Approved && task.AuthorityType is not null)
        {
            await _delegations.EnsureMayApproveAsync(actingForUserId ?? userId, actingRoles, task.AuthorityType,
                instance.TransactionValue ?? 0m, cancellationToken);
        }

        task.Decision = decision;
        task.DecidedByUserId = userId;
        task.DecidedBy = _user.DisplayName ?? _user.Username;
        task.OnBehalfOfUserId = actingForUserId;
        task.OnBehalfOfName = actingForName;
        task.Comment = comment;
        task.DecidedAtUtc = _clock.UtcNow;
        _ledger.Record(instance.EntityType, instance.EntityId, task.StepCode, instance.ProjectId);
        _audit.Write("Workflow", instance.EntityType, instance.EntityId.ToString(), $"Workflow{decision}",
            new { instance.DefinitionCode, instance.DefinitionVersion, task.StepCode, OnBehalfOf = actingForName, instance.TransactionValue },
            comment, task.StepCode);

        var steps = Steps(instance);
        string message;
        if (decision == TaskDecision.Approved)
        {
            var next = steps.FirstOrDefault(s => s.StepOrder > task.StepOrder);
            if (next is not null)
            {
                await CreateTaskAsync(instance, next, null, cancellationToken);
                message = $"Approved. Routed to {next.Name}.";
            }
            else
            {
                instance.State = WorkflowState.Approved;
                instance.CompletedAtUtc = _clock.UtcNow;
                await CompleteAsync(instance, WorkflowState.Approved, comment, cancellationToken);
                message = "Approved. All approval steps are complete.";
            }
        }
        else
        {
            instance.State = decision == TaskDecision.Rejected ? WorkflowState.Rejected : WorkflowState.Returned;
            instance.CompletedAtUtc = _clock.UtcNow;
            await CompleteAsync(instance, instance.State, comment, cancellationToken);
            message = decision == TaskDecision.Rejected ? "Rejected." : "Returned to the originator for rework.";
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new DecisionResult(instance.Id, instance.State.ToString(), message, false);
    }

    public async Task<IReadOnlyList<InboxItemDto>> GetInboxAsync(CancellationToken cancellationToken = default)
    {
        if (_user.UserId is not { } userId) return Array.Empty<InboxItemDto>();

        var capacities = await ActingCapacitiesAsync(userId, cancellationToken);
        var roleCodes = capacities.SelectMany(c => c.Roles).Distinct().ToList();
        if (roleCodes.Count == 0) return Array.Empty<InboxItemDto>();

        var scope = await _scope.GetAsync(cancellationToken);
        var rows = await (from t in _db.WorkflowTasks.AsNoTracking()
                          join i in _db.WorkflowInstances.AsNoTracking() on t.InstanceId equals i.Id
                          where t.Decision == TaskDecision.Pending && i.State == WorkflowState.InProgress
                                && (roleCodes.Contains(t.AssignedRole) || (t.EscalatedToRole != null && roleCodes.Contains(t.EscalatedToRole)))
                          orderby t.DueAtUtc
                          select new { Task = t, Instance = i })
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        return rows
            .Where(r => r.Instance.StartedByUserId != userId)
            .Where(r => r.Instance.ProjectId is null || scope.Includes(r.Instance.ProjectId) || IsOrganisationWide(r.Task.AssignedRole))
            .Select(r =>
            {
                var viaSubstitution = capacities.FirstOrDefault(c => c.PrincipalName is not null
                    && (c.Roles.Contains(r.Task.AssignedRole) || (r.Task.EscalatedToRole != null && c.Roles.Contains(r.Task.EscalatedToRole))));
                var ownCapacity = capacities.Any(c => c.PrincipalName is null
                    && (c.Roles.Contains(r.Task.AssignedRole) || (r.Task.EscalatedToRole != null && c.Roles.Contains(r.Task.EscalatedToRole))));
                return new InboxItemDto(r.Task.Id, r.Instance.Id, r.Instance.DefinitionCode, r.Instance.EntityType, r.Instance.EntityId,
                    r.Instance.EntityReference, r.Instance.Title, r.Instance.TransactionValue, r.Task.StepName, r.Task.AssignedRole,
                    r.Task.DueAtUtc, r.Task.DueAtUtc < now, r.Task.EscalationLevel,
                    ownCapacity ? null : viaSubstitution?.PrincipalName, r.Instance.StartedBy, r.Instance.StartedAtUtc, r.Instance.ProjectId);
            })
            .ToList();
    }

    public async Task<WorkflowInstanceDto> GetAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _db.WorkflowInstances.AsNoTracking().Include(i => i.Tasks)
                           .SingleOrDefaultAsync(i => i.Id == instanceId, cancellationToken)
                       ?? throw new NotFoundException("Workflow", instanceId);
        if (instance.ProjectId is { } pid) await _scope.EnsureProjectAsync(pid, cancellationToken);
        return ToDto(instance);
    }

    public async Task<IReadOnlyList<WorkflowInstanceDto>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default)
    {
        var instances = await _db.WorkflowInstances.AsNoTracking().Include(i => i.Tasks)
            .Where(i => i.EntityType == entityType && i.EntityId == entityId)
            .OrderByDescending(i => i.StartedAtUtc)
            .ToListAsync(cancellationToken);
        return instances.Select(ToDto).ToList();
    }

    public async Task<int> EscalateOverdueAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var overdue = await _db.WorkflowTasks
            .Where(t => t.Decision == TaskDecision.Pending && t.DueAtUtc < now && t.EscalationLevel < 2
                        && (t.EscalatedAtUtc == null || t.EscalatedAtUtc < now.AddDays(-1)))
            .ToListAsync(cancellationToken);
        var level2Role = await _settings.GetAsync(SettingKeys.EscalationRoleLevel2, cancellationToken);

        foreach (var task in overdue)
        {
            var instance = await _db.WorkflowInstances.SingleAsync(i => i.Id == task.InstanceId, cancellationToken);
            if (instance.State != WorkflowState.InProgress) continue;
            var step = Steps(instance).FirstOrDefault(s => s.StepOrder == task.StepOrder);

            task.EscalationLevel++;
            task.EscalatedAtUtc = now;
            task.EscalatedToRole = task.EscalationLevel == 1 ? step?.EscalationRole ?? level2Role : level2Role;

            _db.EscalationEvents.Add(new EscalationEvent
            {
                EntityType = "WorkflowTask",
                EntityId = task.Id,
                EntityReference = instance.EntityReference,
                Level = task.EscalationLevel,
                EscalatedToRole = task.EscalatedToRole,
                Reason = $"Approval step '{task.StepName}' overdue since {task.DueAtUtc:yyyy-MM-dd HH:mm} UTC",
                OccurredAtUtc = now
            });
            _audit.Write("Workflow", instance.EntityType, instance.EntityId.ToString(), "WorkflowEscalated",
                new { task.StepCode, task.EscalationLevel, task.EscalatedToRole }, workflowStep: task.StepCode);
            await _notifier.NotifyRoleAsync(task.EscalatedToRole, NotificationTemplates.WorkflowEscalated,
                new Dictionary<string, string?> { ["Title"] = instance.Title, ["Step"] = task.StepName, ["Reference"] = instance.EntityReference },
                "inbox", instance.EntityType, instance.EntityId, instance.ProjectId, cancellationToken);
        }

        if (overdue.Count > 0) await _db.SaveChangesAsync(cancellationToken);
        return overdue.Count;
    }

    public async Task CancelForEntityAsync(string entityType, Guid entityId, string reason, CancellationToken cancellationToken = default)
    {
        var running = await _db.WorkflowInstances.Include(i => i.Tasks)
            .Where(i => i.EntityType == entityType && i.EntityId == entityId && i.State == WorkflowState.InProgress)
            .ToListAsync(cancellationToken);
        foreach (var instance in running)
        {
            instance.State = WorkflowState.Cancelled;
            instance.CompletedAtUtc = _clock.UtcNow;
            foreach (var t in instance.Tasks.Where(t => t.Decision == TaskDecision.Pending))
            {
                t.Decision = TaskDecision.Cancelled;
                t.Comment = reason;
                t.DecidedAtUtc = _clock.UtcNow;
            }
            _audit.Write("Workflow", entityType, entityId.ToString(), "WorkflowCancelled", null, reason);
        }
    }

    // ---- internals ----

    private async Task CreateTaskAsync(WorkflowInstance instance, StepSnapshot step, string? link, CancellationToken cancellationToken)
    {
        var holidays = await _db.PublicHolidays.AsNoTracking().Select(h => h.Date).ToListAsync(cancellationToken);
        var calendar = new BusinessCalendar(holidays);
        var task = new WorkflowTask
        {
            InstanceId = instance.Id,
            StepOrder = step.StepOrder,
            StepCode = step.Code,
            StepName = step.Name,
            AssignedRole = step.RequiredRole,
            AuthorityType = step.AuthorityType,
            CreatedAtUtc = _clock.UtcNow,
            DueAtUtc = calendar.AddSlaHours(_clock.UtcNow, step.SlaHours)
        };
        instance.CurrentStepOrder = step.StepOrder;
        instance.Tasks.Add(task);
        _db.WorkflowTasks.Add(task);

        await _notifier.NotifyRoleAsync(step.RequiredRole, NotificationTemplates.WorkflowTaskAssigned,
            new Dictionary<string, string?>
            {
                ["Title"] = instance.Title,
                ["Step"] = step.Name,
                ["Reference"] = instance.EntityReference,
                ["DueDate"] = task.DueAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            },
            link ?? "inbox", instance.EntityType, instance.EntityId, instance.ProjectId, cancellationToken);
    }

    private async Task CompleteAsync(WorkflowInstance instance, WorkflowState outcome, string? comment, CancellationToken cancellationToken)
    {
        var handler = _handlers.FirstOrDefault(h => h.EntityType == instance.EntityType);
        if (handler is not null)
        {
            await handler.OnCompletedAsync(instance, outcome, comment, cancellationToken);
        }

        if (instance.StartedByUserId is { } starter)
        {
            await _notifier.NotifyUsersAsync(new[] { starter }, NotificationTemplates.WorkflowCompleted,
                new Dictionary<string, string?>
                {
                    ["Title"] = instance.Title,
                    ["Reference"] = instance.EntityReference,
                    ["Outcome"] = outcome.ToString().ToLowerInvariant(),
                    ["Comment"] = comment
                },
                null, instance.EntityType, instance.EntityId, cancellationToken);
        }
    }

    private async Task<DecisionResult> EscalateForSodAsync(WorkflowInstance instance, WorkflowTask task, SodEscalationRequired sod, CancellationToken cancellationToken)
    {
        var role = await _settings.GetAsync(SettingKeys.EscalationRoleLevel2, cancellationToken);
        task.EscalationLevel++;
        task.EscalatedAtUtc = _clock.UtcNow;
        task.EscalatedToRole = role;
        _db.EscalationEvents.Add(new EscalationEvent
        {
            EntityType = instance.EntityType,
            EntityId = instance.EntityId,
            EntityReference = instance.EntityReference,
            Level = task.EscalationLevel,
            EscalatedToRole = role,
            Reason = $"Segregation-of-duties rule {sod.Rule.Code}: {sod.Rule.Description}",
            OccurredAtUtc = _clock.UtcNow
        });
        _audit.Write("Workflow", instance.EntityType, instance.EntityId.ToString(), "SodEscalated",
            new { sod.Rule.Code, task.StepCode, EscalatedTo = role }, sod.Rule.Description, task.StepCode);
        await _notifier.NotifyRoleAsync(role, NotificationTemplates.WorkflowEscalated,
            new Dictionary<string, string?> { ["Title"] = instance.Title, ["Step"] = task.StepName, ["Reference"] = instance.EntityReference },
            "inbox", instance.EntityType, instance.EntityId, instance.ProjectId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return new DecisionResult(instance.Id, instance.State.ToString(),
            $"Segregation of duties ({sod.Rule.Code}) prevents you from deciding this step; it has been escalated to {role} for an approved exception.", true);
    }

    private sealed record Capacity(Guid? PrincipalUserId, string? PrincipalName, IReadOnlyList<string> Roles);

    private async Task<List<Capacity>> ActingCapacitiesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var capacities = new List<Capacity> { new(null, null, await _roles.GetEffectiveRolesAsync(userId, cancellationToken)) };
        var now = _clock.UtcNow;
        var substitutions = await _db.Substitutions.AsNoTracking()
            .Where(s => s.SubstituteUserId == userId && !s.IsRevoked && s.FromUtc <= now && s.ToUtc >= now)
            .ToListAsync(cancellationToken);
        foreach (var s in substitutions)
        {
            capacities.Add(new Capacity(s.PrincipalUserId, s.PrincipalName, await _roles.GetEffectiveRolesAsync(s.PrincipalUserId, cancellationToken)));
        }
        return capacities;
    }

    private async Task<(Guid? ForUserId, string? ForName, IReadOnlyCollection<string> Roles)> ResolveActingCapacityAsync(
        Guid userId, WorkflowTask task, CancellationToken cancellationToken)
    {
        var capacities = await ActingCapacitiesAsync(userId, cancellationToken);
        bool Covers(Capacity c) => c.Roles.Contains(task.AssignedRole) || (task.EscalatedToRole is not null && c.Roles.Contains(task.EscalatedToRole));

        var own = capacities.FirstOrDefault(c => c.PrincipalUserId is null && Covers(c));
        if (own is not null) return (null, null, own.Roles);

        var substitute = capacities.FirstOrDefault(c => c.PrincipalUserId is not null && Covers(c));
        if (substitute is not null) return (substitute.PrincipalUserId, substitute.PrincipalName, substitute.Roles);

        throw new ForbiddenException($"Only a holder of role '{task.AssignedRole}' (or their appointed substitute) can decide this step.");
    }

    private static bool IsOrganisationWide(string role) => Domain.Security.Roles.OrganisationWide.Contains(role);

    private static List<StepSnapshot> Steps(WorkflowInstance instance) =>
        JsonSerializer.Deserialize<List<StepSnapshot>>(instance.StepsSnapshotJson, Json) ?? new List<StepSnapshot>();

    private WorkflowInstanceDto ToDto(WorkflowInstance i)
    {
        var now = _clock.UtcNow;
        return new WorkflowInstanceDto(i.Id, i.DefinitionCode, i.DefinitionVersion, i.EntityType, i.EntityId, i.EntityReference, i.Title,
            i.TransactionValue, i.State.ToString(), i.CurrentStepOrder, i.StartedAtUtc, i.StartedBy, i.CompletedAtUtc, Steps(i),
            i.Tasks.OrderBy(t => t.StepOrder).ThenBy(t => t.CreatedAtUtc).Select(t => new WorkflowTaskDto(
                t.Id, t.StepOrder, t.StepCode, t.StepName, t.AssignedRole, t.AuthorityType, t.CreatedAtUtc, t.DueAtUtc,
                t.Decision.ToString(), t.DecidedBy, t.OnBehalfOfName, t.Comment, t.DecidedAtUtc, t.EscalationLevel,
                t.EscalatedToRole, t.Decision == TaskDecision.Pending && t.DueAtUtc < now)).ToList());
    }
}
