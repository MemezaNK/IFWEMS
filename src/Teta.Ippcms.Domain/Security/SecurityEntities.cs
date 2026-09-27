using Platform.Audit;
using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Security;

/// <summary>TETA application role (SRS §10). IsPrivileged roles must use MFA (SEC-002).</summary>
public class TetaRole : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsPrivileged { get; set; }
    public bool HasApprovalAuthority { get; set; }
}

public class RolePermission : Entity
{
    public string RoleCode { get; set; } = default!;
    public string Permission { get; set; } = default!;
}

public enum ScopeType
{
    Global,
    Portfolio,
    Programme,
    Project
}

public enum AssignmentStatus
{
    PendingApproval,
    Active,
    Rejected,
    Revoked
}

/// <summary>
/// Scoped, effective-dated role assignment for a shared platform user (SRS §7 "UserRole", SEC-003/004).
/// Assignments made by a Security Administrator need a second Security Administrator's approval
/// (dual control, SRS §10).
/// </summary>
public class UserRoleAssignment : AuditableEntity
{
    public Guid UserId { get; set; }
    public string RoleCode { get; set; } = default!;
    public ScopeType ScopeType { get; set; } = ScopeType.Global;
    public Guid? ScopeId { get; set; }
    public string? ScopeName { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.PendingApproval;
    public Guid? RequestedByUserId { get; set; }
    public string? RequestedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? Reason { get; set; }

    public bool IsEffectiveOn(DateOnly date) =>
        Status == AssignmentStatus.Active && EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo >= date);
}

/// <summary>Per-user TETA security profile: MFA enrolment (secret encrypted at rest).</summary>
public class UserSecurityProfile : AuditableEntity
{
    public Guid UserId { get; set; }

    [SensitiveData]
    public string? MfaSecretProtected { get; set; }
    public bool MfaEnabled { get; set; }
    public DateTime? MfaEnrolledAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
}

/// <summary>Server-side session record enabling inactivity timeout and revocation (SEC-009).</summary>
[NotAudited]
public class UserSession : Entity
{
    public Guid UserId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool MfaSatisfied { get; set; }

    public bool IsActive(DateTime utcNow, TimeSpan inactivityTimeout) =>
        RevokedAtUtc is null && ExpiresAtUtc > utcNow && LastSeenAtUtc.Add(inactivityTimeout) > utcNow;
}

/// <summary>
/// Tamper-evident audit record (SRS §14, §25.2, SEC-010). Append-only: the application refuses
/// updates/deletes and the database trigger blocks them; each row carries an HMAC seal.
/// </summary>
[NotAudited]
public class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string CorrelationId { get; set; } = default!;
    public string? SessionId { get; set; }
    public string Module { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string? EntityId { get; set; }
    public string Action { get; set; } = default!;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? WorkflowStep { get; set; }
    public string? Reason { get; set; }
    public string? SourceIp { get; set; }
    public string? UserAgent { get; set; }
    public string Seal { get; set; } = default!;
}
