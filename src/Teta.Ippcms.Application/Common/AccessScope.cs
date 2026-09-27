using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Domain.Projects;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Application.Common;

/// <summary>The set of projects the current user may see (row-level security, SEC-004).</summary>
public sealed class DataScope
{
    public static readonly DataScope Everything = new(true, new HashSet<Guid>());
    public static readonly DataScope Nothing = new(false, new HashSet<Guid>());

    public DataScope(bool all, IReadOnlySet<Guid> projectIds)
    {
        All = all;
        ProjectIds = projectIds;
    }

    public bool All { get; }
    public IReadOnlySet<Guid> ProjectIds { get; }

    /// <summary>List form for EF Core query translation (Contains on a List translates on all providers).</summary>
    public List<Guid> ProjectIdList => ProjectIds.ToList();

    public bool Includes(Guid? projectId) => All || (projectId.HasValue && ProjectIds.Contains(projectId.Value));
}

public interface IAccessScope
{
    Task<DataScope> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Throws NotFound when the project is outside the caller's scope (existence is not disclosed).</summary>
    Task EnsureProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public sealed class AccessScopeService : IAccessScope
{
    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private DataScope? _cached;

    public AccessScopeService(ITetaDbContext db, ICurrentUser user, IClock clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task<DataScope> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null) return _cached;
        if (_user.UserId is not { } userId) return _cached = DataScope.Nothing;
        return _cached = await ForUserAsync(_db, userId, _clock.Today, cancellationToken);
    }

    /// <summary>Computes the project scope of any user (used for scheduled reports run on the owner's behalf).</summary>
    public static async Task<DataScope> ForUserAsync(ITetaDbContext db, Guid userId, DateOnly today, CancellationToken cancellationToken = default)
    {
        var assignments = await db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.Status == AssignmentStatus.Active
                        && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .ToListAsync(cancellationToken);

        // Supplier and evaluator access is governed separately (portal ownership / committee membership).
        var businessAssignments = assignments.Where(a => a.RoleCode is not (Roles.Supplier or Roles.Evaluator)).ToList();
        if (businessAssignments.Any(a => a.ScopeType == ScopeType.Global))
        {
            return DataScope.Everything;
        }

        var portfolioIds = businessAssignments.Where(a => a.ScopeType == ScopeType.Portfolio && a.ScopeId != null).Select(a => a.ScopeId!.Value).ToList();
        var programmeIds = businessAssignments.Where(a => a.ScopeType == ScopeType.Programme && a.ScopeId != null).Select(a => a.ScopeId!.Value).ToList();
        var projectIds = businessAssignments.Where(a => a.ScopeType == ScopeType.Project && a.ScopeId != null).Select(a => a.ScopeId!.Value).ToHashSet();

        if (portfolioIds.Count > 0)
        {
            var viaPortfolio = await db.Projects.AsNoTracking()
                .Where(p => db.Programmes.Any(pr => pr.Id == p.ProgrammeId && portfolioIds.Contains(pr.PortfolioId)))
                .Select(p => p.Id).ToListAsync(cancellationToken);
            projectIds.UnionWith(viaPortfolio);
        }
        if (programmeIds.Count > 0)
        {
            var viaProgramme = await db.Projects.AsNoTracking()
                .Where(p => programmeIds.Contains(p.ProgrammeId)).Select(p => p.Id).ToListAsync(cancellationToken);
            projectIds.UnionWith(viaProgramme);
        }

        // Projects the user manages/sponsors or is an effective stakeholder on.
        var owned = await db.Projects.AsNoTracking()
            .Where(p => p.ManagerUserId == userId || p.SponsorUserId == userId || p.CreatedByUserId == userId)
            .Select(p => p.Id).ToListAsync(cancellationToken);
        projectIds.UnionWith(owned);

        var stakeholderOf = await db.ProjectStakeholders.AsNoTracking()
            .Where(s => s.UserId == userId && s.EffectiveFrom <= today && (s.EffectiveTo == null || s.EffectiveTo >= today))
            .Select(s => s.ProjectId).ToListAsync(cancellationToken);
        projectIds.UnionWith(stakeholderOf);

        return new DataScope(false, projectIds);
    }

    public async Task EnsureProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var scope = await GetAsync(cancellationToken);
        if (!scope.Includes(projectId)) throw new NotFoundException(nameof(Project), projectId);
    }
}

public static class ScopeQueryExtensions
{
    /// <summary>Applies the row-level project scope to any query over records with a ProjectId.</summary>
    public static IQueryable<T> InScope<T>(this IQueryable<T> query, DataScope scope, System.Linq.Expressions.Expression<Func<T, Guid>> projectId)
    {
        if (scope.All) return query;
        var ids = scope.ProjectIdList;
        var parameter = projectId.Parameters[0];
        var contains = System.Linq.Expressions.Expression.Call(
            typeof(Enumerable), nameof(Enumerable.Contains), new[] { typeof(Guid) },
            System.Linq.Expressions.Expression.Constant(ids), projectId.Body);
        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(contains, parameter));
    }

    public static IQueryable<T> InScopeNullable<T>(this IQueryable<T> query, DataScope scope, System.Linq.Expressions.Expression<Func<T, Guid?>> projectId)
    {
        if (scope.All) return query;
        var ids = scope.ProjectIdList.Select(g => (Guid?)g).ToList();
        var parameter = projectId.Parameters[0];
        var contains = System.Linq.Expressions.Expression.Call(
            typeof(Enumerable), nameof(Enumerable.Contains), new[] { typeof(Guid?) },
            System.Linq.Expressions.Expression.Constant(ids), projectId.Body);
        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(contains, parameter));
    }
}
