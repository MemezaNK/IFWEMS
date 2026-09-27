using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.Application.Common;

/// <summary>Effective (active, in-date) TETA roles for a user.</summary>
public interface IUserRoles
{
    Task<IReadOnlyList<string>> GetEffectiveRolesAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class UserRolesService : IUserRoles
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;

    public UserRolesService(ITetaDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<string>> GetEffectiveRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        return await _db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.Status == AssignmentStatus.Active
                        && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .Select(a => a.RoleCode).Distinct().ToListAsync(cancellationToken);
    }
}

/// <summary>Delegation-of-authority checks (FR-ADM-002, BR-007).</summary>
public interface IDelegationService
{
    /// <summary>Highest amount the user may approve for the authority type today (null = no authority).</summary>
    Task<decimal?> GetLimitAsync(Guid userId, IReadOnlyCollection<string> roles, string authorityType, CancellationToken cancellationToken = default);

    Task EnsureMayApproveAsync(Guid userId, IReadOnlyCollection<string> roles, string authorityType, decimal amount, CancellationToken cancellationToken = default);
}

public sealed class DelegationService : IDelegationService
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;

    public DelegationService(ITetaDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<decimal?> GetLimitAsync(Guid userId, IReadOnlyCollection<string> roles, string authorityType, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var roleList = roles.ToList();
        var limits = await _db.Delegations.AsNoTracking()
            .Where(d => d.AuthorityType == authorityType && d.IsActive
                        && d.EffectiveFrom <= today && (d.EffectiveTo == null || d.EffectiveTo >= today)
                        && (d.UserId == userId || (d.RoleCode != null && roleList.Contains(d.RoleCode))))
            .Select(d => d.MaxAmount)
            .ToListAsync(cancellationToken);
        return limits.Count == 0 ? null : limits.Max();
    }

    public async Task EnsureMayApproveAsync(Guid userId, IReadOnlyCollection<string> roles, string authorityType, decimal amount, CancellationToken cancellationToken = default)
    {
        var limit = await GetLimitAsync(userId, roles, authorityType, cancellationToken);
        if (limit is null)
            throw new DomainException($"You have no current delegated authority for {authorityType}.", "BR-007");
        if (amount > limit.Value)
            throw new DomainException(
                $"The transaction value R {amount:N2} exceeds your delegated authority of R {limit.Value:N2} for {authorityType}.", "BR-007");
    }
}

/// <summary>Records who performed which material action on a transaction (ledger for SoD checks).</summary>
public interface ITransactionLedger
{
    void Record(string entityType, Guid entityId, string actionCode, Guid? projectId = null);
}

public sealed class TransactionLedger : ITransactionLedger
{
    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public TransactionLedger(ITetaDbContext db, ICurrentUser user, IClock clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public void Record(string entityType, Guid entityId, string actionCode, Guid? projectId = null) =>
        _db.TransactionActions.Add(new TransactionAction
        {
            EntityType = entityType,
            EntityId = entityId,
            ActionCode = actionCode,
            UserId = _user.UserId,
            Username = _user.Username,
            OccurredAtUtc = _clock.UtcNow,
            ProjectId = projectId
        });
}

/// <summary>Raised when a configured SoD rule requires escalation instead of blocking outright.</summary>
public sealed class SodEscalationRequired : Exception
{
    public SodEscalationRequired(SodRule rule) : base(rule.Description) => Rule = rule;
    public SodRule Rule { get; }
}

/// <summary>Segregation-of-duties enforcement (SEC-005, BR-008).</summary>
public interface ISodService
{
    /// <summary>
    /// Throws <see cref="DomainException"/> (BR-008) when the user already performed a conflicting
    /// action on the same transaction and the rule blocks, or <see cref="SodEscalationRequired"/> when
    /// the rule routes to an approved exception process.
    /// </summary>
    Task EnsureAllowedAsync(string entityType, Guid entityId, string actionCode, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class SodService : ISodService
{
    private readonly ITetaDbContext _db;

    public SodService(ITetaDbContext db) => _db = db;

    public async Task EnsureAllowedAsync(string entityType, Guid entityId, string actionCode, Guid userId, CancellationToken cancellationToken = default)
    {
        var rules = await _db.SodRules.AsNoTracking()
            .Where(r => r.IsActive && r.EntityType == entityType && r.SecondAction == actionCode)
            .ToListAsync(cancellationToken);
        if (rules.Count == 0) return;

        var firstActions = rules.Select(r => r.FirstAction).ToList();
        var performed = await _db.TransactionActions.AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId && a.UserId == userId && firstActions.Contains(a.ActionCode))
            .Select(a => a.ActionCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Include pending (unsaved) ledger entries from this unit of work.
        var pending = _db.TransactionActions.Local
            .Where(a => a.EntityType == entityType && a.EntityId == entityId && a.UserId == userId && firstActions.Contains(a.ActionCode))
            .Select(a => a.ActionCode);
        var all = performed.Concat(pending).ToHashSet();

        var violated = rules.FirstOrDefault(r => all.Contains(r.FirstAction));
        if (violated is null) return;

        if (violated.Mode == SodMode.Escalate) throw new SodEscalationRequired(violated);
        throw new DomainException($"Segregation of duties: {violated.Description}", "BR-008");
    }
}
