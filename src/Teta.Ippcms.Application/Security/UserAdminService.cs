using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Platform.Security.Identity;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Application.Security;

public sealed record UserSummaryDto(Guid Id, string Username, string Email, string DisplayName, bool IsActive, bool LockedOut, bool MfaEnabled,
    DateTime? LastLoginAtUtc, IReadOnlyList<AssignmentDto> Assignments, bool HasTetaAccess);
public sealed record AssignmentDto(Guid Id, Guid UserId, string RoleCode, string ScopeType, Guid? ScopeId, string? ScopeName, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, string Status, string? RequestedBy, string? ApprovedBy, DateTime? ApprovedAtUtc, string? Reason, bool IsEffective);
public sealed record CreateUserRequest(string Username, string Email, string DisplayName, string InitialPassword);
public sealed record RequestAssignmentRequest(Guid UserId, string RoleCode, ScopeType ScopeType, Guid? ScopeId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Reason);
public sealed record DecideAssignmentRequest(bool Approve, string? Comment);
public sealed record RoleDto(string Code, string Name, string? Description, bool IsPrivileged, bool HasApprovalAuthority, IReadOnlyList<string> Permissions);
public sealed record SaveRolePermissionsRequest(IReadOnlyList<string> Permissions);
public sealed record SessionDto(Guid Id, Guid UserId, string? Username, DateTime IssuedAtUtc, DateTime LastSeenAtUtc, DateTime ExpiresAtUtc, bool Active,
    string? IpAddress, bool MfaSatisfied);
public sealed record UserLookupDto(Guid Id, string DisplayName, string Username, string Email);

public interface IUserAdminService
{
    Task<PagedResult<UserSummaryDto>> ListUsersAsync(string? search, bool tetaOnly, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<UserLookupDto>> LookupAsync(string? search, string? roleCode, CancellationToken ct);
    Task<UserSummaryDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserSummaryDto> SetActiveAsync(Guid userId, bool active, string reason, CancellationToken ct);
    Task<UserSummaryDto> SetMfaAsync(Guid userId, bool enabled, string reason, CancellationToken ct);
    Task<UserSummaryDto> UnlockAsync(Guid userId, CancellationToken ct);
    Task<AssignmentDto> RequestAssignmentAsync(RequestAssignmentRequest request, CancellationToken ct);
    Task<AssignmentDto> DecideAssignmentAsync(Guid assignmentId, DecideAssignmentRequest request, CancellationToken ct);
    Task<AssignmentDto> RevokeAssignmentAsync(Guid assignmentId, string reason, CancellationToken ct);
    Task<IReadOnlyList<AssignmentDto>> PendingAssignmentsAsync(CancellationToken ct);
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct);
    Task<RoleDto> SaveRolePermissionsAsync(string roleCode, SaveRolePermissionsRequest request, CancellationToken ct);
    Task<IReadOnlyList<SessionDto>> ListSessionsAsync(Guid? userId, CancellationToken ct);
    Task RevokeSessionAsync(Guid sessionId, string reason, CancellationToken ct);
}

/// <summary>
/// Identity and role administration (SRS §10 Security Administrator): scoped, effective-dated role
/// assignments with dual control — a second Security Administrator approves (SEC-003/004/005).
/// </summary>
public sealed class UserAdminService : IUserAdminService
{
    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IPlatformPasswordHasher _hasher;
    private readonly IAuditWriter _audit;

    public UserAdminService(ITetaDbContext db, ICurrentUser user, IClock clock, IPlatformPasswordHasher hasher, IAuditWriter audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _hasher = hasher;
        _audit = audit;
    }

    public async Task<PagedResult<UserSummaryDto>> ListUsersAsync(string? search, bool tetaOnly, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(u => u.Username.Contains(search) || u.DisplayName.Contains(search) || u.Email.Contains(search));
        if (tetaOnly) q = q.Where(u => _db.UserRoleAssignments.Any(a => a.UserId == u.Id));
        var total = await q.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var users = await q.OrderBy(u => u.DisplayName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<UserSummaryDto>(await ToDtosAsync(users, ct), total, page, pageSize);
    }

    public async Task<IReadOnlyList<UserLookupDto>> LookupAsync(string? search, string? roleCode, CancellationToken ct)
    {
        var q = _db.Users.AsNoTracking().Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(u => u.DisplayName.Contains(search) || u.Username.Contains(search));
        if (!string.IsNullOrWhiteSpace(roleCode)) q = q.Where(u => _db.UserRoleAssignments.Any(a => a.UserId == u.Id && a.RoleCode == roleCode && a.Status == AssignmentStatus.Active));
        else q = q.Where(u => _db.UserRoleAssignments.Any(a => a.UserId == u.Id && a.Status == AssignmentStatus.Active));
        return await q.OrderBy(u => u.DisplayName).Take(50).Select(u => new UserLookupDto(u.Id, u.DisplayName, u.Username, u.Email)).ToListAsync(ct);
    }

    public async Task<UserSummaryDto> CreateUserAsync(CreateUserRequest r, CancellationToken ct)
    {
        new Validator().Required("username", r.Username, 100).Required("email", r.Email, 256).Required("displayName", r.DisplayName, 200)
            .Must(r.Email.Contains('@'), "email", "Enter a valid e-mail address.").ThrowIfInvalid();
        var errors = PasswordPolicy.Validate(r.InitialPassword);
        if (errors.Count > 0) throw new ValidationException(new Dictionary<string, string[]> { ["initialPassword"] = errors.ToArray() });
        if (await _db.Users.AnyAsync(u => u.Username == r.Username || u.Email == r.Email, ct))
            throw new ConflictException("A user with this username or e-mail already exists in the shared identity store. Assign TETA roles to the existing account instead.");
        var user = new PlatformUser
        {
            Username = r.Username.Trim(), Email = r.Email.Trim(), DisplayName = r.DisplayName.Trim(), PasswordHash = _hasher.Hash(r.InitialPassword),
            IsActive = true, CreatedAtUtc = _clock.UtcNow, CreatedBy = _user.Username
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { user }, ct))[0];
    }

    public async Task<UserSummaryDto> SetActiveAsync(Guid userId, bool active, string reason, CancellationToken ct)
    {
        new Validator().Required("reason", reason, 500).ThrowIfInvalid();
        if (userId == _user.UserId) throw new DomainException("You cannot change your own account status.", "SEC-005");
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
        user.IsActive = active;
        user.ModifiedAtUtc = _clock.UtcNow;
        user.ModifiedBy = _user.Username;
        if (!active) await RevokeAllSessionsAsync(userId, "Account deactivated", ct);
        _audit.Write("Security", "User", userId.ToString(), active ? "UserActivated" : "UserDeactivated", new { user.Username }, reason);
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { user }, ct))[0];
    }

    public async Task<UserSummaryDto> SetMfaAsync(Guid userId, bool enabled, string reason, CancellationToken ct)
    {
        new Validator().Required("reason", reason, 500).ThrowIfInvalid();
        if (userId == _user.UserId) throw new DomainException("You cannot change your own MFA setting.", "SEC-005");
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
        var profile = await _db.UserSecurityProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct) ?? new UserSecurityProfile { UserId = userId };
        if (profile.UserId == Guid.Empty) _db.UserSecurityProfiles.Add(profile);

        if (enabled && string.IsNullOrWhiteSpace(profile.MfaSecretProtected))
            throw new DomainException("This user has not completed MFA enrolment yet. Ask them to sign in and set up an authenticator app first.", "SEC-002");

        profile.MfaEnabled = enabled;
        profile.MfaEnrolledAtUtc = enabled ? (profile.MfaEnrolledAtUtc ?? _clock.UtcNow) : null;
        user.MfaEnabled = enabled;
        user.ModifiedAtUtc = _clock.UtcNow;
        user.ModifiedBy = _user.Username;
        _audit.Write("Security", "User", userId.ToString(), enabled ? "MfaEnabled" : "MfaDisabled",
            new { user.Username, user.MfaEnabled, profile.MfaEnrolledAtUtc }, reason);
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { user }, ct))[0];
    }

    public async Task<UserSummaryDto> UnlockAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
        user.FailedLoginAttempts = 0;
        user.LockedOutUntilUtc = null;
        _audit.Write("Security", "User", userId.ToString(), "UserUnlocked", new { user.Username });
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { user }, ct))[0];
    }

    public async Task<AssignmentDto> RequestAssignmentAsync(RequestAssignmentRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("userId", r.UserId).Required("roleCode", r.RoleCode, 50).Required("reason", r.Reason, 500)
            .DateOrder("effectiveFrom", r.EffectiveFrom, "effectiveTo", r.EffectiveTo)
            .Must(r.ScopeType == ScopeType.Global || r.ScopeId is not null, "scopeId", "Select the portfolio, programme or project for a scoped assignment.")
            .ThrowIfInvalid();
        if (!await _db.Roles.AnyAsync(x => x.Code == r.RoleCode, ct)) throw new ValidationException("roleCode", "Unknown role.");
        if (!await _db.Users.AnyAsync(u => u.Id == r.UserId, ct)) throw new NotFoundException("User", r.UserId);
        if (r.UserId == _user.UserId) throw new DomainException("You cannot assign roles to yourself.", "SEC-005");

        // Incompatible role combinations (SoD matrix): suppliers never hold internal roles; evaluators never adjudicate.
        var existing = await _db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.UserId == r.UserId && (a.Status == AssignmentStatus.Active || a.Status == AssignmentStatus.PendingApproval)).Select(a => a.RoleCode).ToListAsync(ct);
        if (r.RoleCode == Roles.Supplier && existing.Any(x => x != Roles.Supplier) || r.RoleCode != Roles.Supplier && existing.Contains(Roles.Supplier))
            throw new DomainException("Supplier portal accounts cannot hold internal TETA roles.", "SEC-005");
        if ((r.RoleCode == Roles.SecurityAdministrator && existing.Contains(Roles.SystemAdministrator)) ||
            (r.RoleCode == Roles.SystemAdministrator && existing.Contains(Roles.SecurityAdministrator)))
            throw new DomainException("System and Security Administrator roles must be held by different people.", "SEC-005");

        string? scopeName = null;
        if (r.ScopeId is { } sid)
        {
            scopeName = r.ScopeType switch
            {
                ScopeType.Portfolio => await _db.Portfolios.Where(x => x.Id == sid).Select(x => x.Name).SingleOrDefaultAsync(ct),
                ScopeType.Programme => await _db.Programmes.Where(x => x.Id == sid).Select(x => x.Name).SingleOrDefaultAsync(ct),
                ScopeType.Project => await _db.Projects.Where(x => x.Id == sid).Select(x => x.Name).SingleOrDefaultAsync(ct),
                _ => null
            } ?? throw new ValidationException("scopeId", "The selected scope does not exist.");
        }

        var assignment = new UserRoleAssignment
        {
            UserId = r.UserId, RoleCode = r.RoleCode, ScopeType = r.ScopeType, ScopeId = r.ScopeType == ScopeType.Global ? null : r.ScopeId, ScopeName = scopeName,
            EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo, Status = AssignmentStatus.PendingApproval, RequestedByUserId = _user.UserId,
            RequestedBy = _user.DisplayName ?? _user.Username, Reason = r.Reason
        };
        _db.UserRoleAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);
        return ToDto(assignment, _clock.Today);
    }

    public async Task<AssignmentDto> DecideAssignmentAsync(Guid assignmentId, DecideAssignmentRequest r, CancellationToken ct)
    {
        var a = await _db.UserRoleAssignments.SingleOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw new NotFoundException("Assignment", assignmentId);
        if (a.Status != AssignmentStatus.PendingApproval) throw new DomainException("The assignment is not awaiting approval.", "SEC-003");
        if (a.RequestedByUserId == _user.UserId) throw new DomainException("Dual control: a second Security Administrator must approve this assignment.", "SEC-005");
        if (a.UserId == _user.UserId) throw new DomainException("You cannot approve your own role assignment.", "SEC-005");
        if (!r.Approve && string.IsNullOrWhiteSpace(r.Comment)) throw new ValidationException("comment", "A reason is required when rejecting.");
        a.Status = r.Approve ? AssignmentStatus.Active : AssignmentStatus.Rejected;
        a.ApprovedBy = _user.DisplayName ?? _user.Username;
        a.ApprovedAtUtc = _clock.UtcNow;
        _audit.Write("Security", nameof(UserRoleAssignment), a.Id.ToString(), r.Approve ? "RoleAssignmentApproved" : "RoleAssignmentRejected",
            new { a.UserId, a.RoleCode, a.ScopeType, a.ScopeName }, r.Comment);
        await _db.SaveChangesAsync(ct);
        return ToDto(a, _clock.Today);
    }

    public async Task<AssignmentDto> RevokeAssignmentAsync(Guid assignmentId, string reason, CancellationToken ct)
    {
        new Validator().Required("reason", reason, 500).ThrowIfInvalid();
        var a = await _db.UserRoleAssignments.SingleOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw new NotFoundException("Assignment", assignmentId);
        if (a.Status is not (AssignmentStatus.Active or AssignmentStatus.PendingApproval)) throw new DomainException("The assignment is not active.", "SEC-003");
        a.Status = AssignmentStatus.Revoked;
        a.EffectiveTo = _clock.Today.AddDays(-1) < a.EffectiveFrom ? a.EffectiveFrom : _clock.Today.AddDays(-1);
        _audit.Write("Security", nameof(UserRoleAssignment), a.Id.ToString(), "RoleAssignmentRevoked", new { a.UserId, a.RoleCode }, reason);
        await _db.SaveChangesAsync(ct);
        return ToDto(a, _clock.Today);
    }

    public async Task<IReadOnlyList<AssignmentDto>> PendingAssignmentsAsync(CancellationToken ct)
    {
        var today = _clock.Today;
        return (await _db.UserRoleAssignments.AsNoTracking().Where(a => a.Status == AssignmentStatus.PendingApproval).OrderBy(a => a.CreatedAtUtc).ToListAsync(ct))
            .Select(a => ToDto(a, today)).ToList();
    }

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct)
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var perms = await _db.RolePermissions.AsNoTracking().ToListAsync(ct);
        return roles.Select(r => new RoleDto(r.Code, r.Name, r.Description, r.IsPrivileged, r.HasApprovalAuthority,
            perms.Where(p => p.RoleCode == r.Code).Select(p => p.Permission).OrderBy(p => p).ToList())).ToList();
    }

    public async Task<RoleDto> SaveRolePermissionsAsync(string roleCode, SaveRolePermissionsRequest r, CancellationToken ct)
    {
        var role = await _db.Roles.AsNoTracking().SingleOrDefaultAsync(x => x.Code == roleCode, ct) ?? throw new NotFoundException("Role", roleCode);
        var unknown = r.Permissions.Except(Permissions.All).ToList();
        if (unknown.Count > 0) throw new ValidationException("permissions", $"Unknown permission(s): {string.Join(", ", unknown)}.");
        if (roleCode == Roles.SystemAdministrator && r.Permissions.Any(p => p is Permissions.WorkflowDecide or Permissions.FinanceCertify or Permissions.ProjectApprove))
            throw new DomainException("System Administrators administer configuration without business approval authority.", "SEC-005");
        var current = await _db.RolePermissions.Where(x => x.RoleCode == roleCode).ToListAsync(ct);
        foreach (var p in current.Where(p => !r.Permissions.Contains(p.Permission))) _db.RolePermissions.Remove(p);
        foreach (var p in r.Permissions.Distinct().Where(p => current.All(c => c.Permission != p)))
            _db.RolePermissions.Add(new RolePermission { RoleCode = roleCode, Permission = p });
        _audit.Write("Security", nameof(TetaRole), roleCode, "RolePermissionsChanged",
            new { Before = current.Select(c => c.Permission).OrderBy(x => x), After = r.Permissions.OrderBy(x => x) });
        await _db.SaveChangesAsync(ct);
        return (await ListRolesAsync(ct)).Single(x => x.Code == roleCode);
    }

    public async Task<IReadOnlyList<SessionDto>> ListSessionsAsync(Guid? userId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var q = from s in _db.UserSessions.AsNoTracking()
                join u in _db.Users.AsNoTracking() on s.UserId equals u.Id
                select new { s, u.Username };
        if (userId is { } uid) q = q.Where(x => x.s.UserId == uid);
        else q = q.Where(x => x.s.RevokedAtUtc == null && x.s.ExpiresAtUtc > now);
        return (await q.OrderByDescending(x => x.s.LastSeenAtUtc).Take(500).ToListAsync(ct))
            .Select(x => new SessionDto(x.s.Id, x.s.UserId, x.Username, x.s.IssuedAtUtc, x.s.LastSeenAtUtc, x.s.ExpiresAtUtc,
                x.s.RevokedAtUtc is null && x.s.ExpiresAtUtc > now, x.s.IpAddress, x.s.MfaSatisfied)).ToList();
    }

    public async Task RevokeSessionAsync(Guid sessionId, string reason, CancellationToken ct)
    {
        new Validator().Required("reason", reason, 500).ThrowIfInvalid();
        var s = await _db.UserSessions.SingleOrDefaultAsync(x => x.Id == sessionId, ct) ?? throw new NotFoundException("Session", sessionId);
        s.RevokedAtUtc ??= _clock.UtcNow;
        s.RevokedReason = reason;
        _audit.Write("Security", "Session", sessionId.ToString(), "SessionRevoked", new { s.UserId }, reason);
        await _db.SaveChangesAsync(ct);
    }

    private async Task RevokeAllSessionsAsync(Guid userId, string reason, CancellationToken ct)
    {
        foreach (var s in await _db.UserSessions.Where(x => x.UserId == userId && x.RevokedAtUtc == null).ToListAsync(ct))
        {
            s.RevokedAtUtc = _clock.UtcNow;
            s.RevokedReason = reason;
        }
    }

    private async Task<List<UserSummaryDto>> ToDtosAsync(IReadOnlyCollection<PlatformUser> users, CancellationToken ct)
    {
        var ids = users.Select(u => u.Id).ToList();
        var assignments = await _db.UserRoleAssignments.AsNoTracking().Where(a => ids.Contains(a.UserId)).ToListAsync(ct);
        var profiles = await _db.UserSecurityProfiles.AsNoTracking().Where(p => ids.Contains(p.UserId)).ToListAsync(ct);
        var today = _clock.Today;
        var now = _clock.UtcNow;
        return users.Select(u =>
        {
            var mine = assignments.Where(a => a.UserId == u.Id).OrderBy(a => a.RoleCode).Select(a => ToDto(a, today)).ToList();
            var profile = profiles.FirstOrDefault(p => p.UserId == u.Id);
            return new UserSummaryDto(u.Id, u.Username, u.Email, u.DisplayName, u.IsActive, u.IsLockedOut(now), profile?.MfaEnabled == true || u.MfaEnabled,
                profile?.LastLoginAtUtc, mine, mine.Any(a => a.IsEffective));
        }).ToList();
    }

    private static AssignmentDto ToDto(UserRoleAssignment a, DateOnly today) =>
        new(a.Id, a.UserId, a.RoleCode, a.ScopeType.ToString(), a.ScopeId, a.ScopeName, a.EffectiveFrom, a.EffectiveTo, a.Status.ToString(), a.RequestedBy,
            a.ApprovedBy, a.ApprovedAtUtc, a.Reason, a.IsEffectiveOn(today));
}
