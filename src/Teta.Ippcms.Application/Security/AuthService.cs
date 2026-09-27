using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Platform.Core;
using Platform.Security.Crypto;
using Platform.Security.Identity;
using Platform.Security.Mfa;
using Platform.Security.Tokens;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Application.Security;

public sealed record LoginRequest(string Username, string Password);
public sealed record MfaVerifyRequest(string ChallengeToken, string Code);
public sealed record MfaEnrolmentDto(string Secret, string OtpAuthUri, string ChallengeToken);
public sealed record MfaConfirmRequest(string ChallengeToken, string Code);

/// <summary>Result of a login step. Exactly one of Token / Challenge is set when Succeeded.</summary>
public sealed record LoginResult(
    bool Succeeded,
    string? AccessToken,
    DateTime? ExpiresAtUtc,
    string? ChallengeToken,
    bool MfaRequired,
    bool MfaEnrolmentRequired,
    string? Message,
    CurrentUserDto? User);

public sealed record CurrentUserDto(Guid UserId, string Username, string DisplayName, string Email, IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions, IReadOnlyList<RoleScopeDto> Scopes, Guid? SupplierId, bool MfaEnabled, bool MfaRequired, int InactivityMinutes);
public sealed record RoleScopeDto(string RoleCode, string ScopeType, Guid? ScopeId, string? ScopeName);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken ct);
    Task<LoginResult> VerifyMfaAsync(MfaVerifyRequest request, string? ipAddress, string? userAgent, CancellationToken ct);
    Task<MfaEnrolmentDto> BeginMfaEnrolmentAsync(string? challengeToken, CancellationToken ct);
    Task<LoginResult> ConfirmMfaEnrolmentAsync(MfaConfirmRequest request, string? ipAddress, string? userAgent, CancellationToken ct);
    Task<LoginResult> RefreshAsync(CancellationToken ct);
    Task LogoutAsync(CancellationToken ct);
    Task<CurrentUserDto> MeAsync(CancellationToken ct);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);
    Task<IReadOnlyList<string>> PermissionsForRolesAsync(IReadOnlyCollection<string> roles, CancellationToken ct);
    Task<bool> ValidateSessionAsync(Guid userId, string sessionId, CancellationToken ct);
}

/// <summary>
/// TETA authentication against the shared platform identity store (dbo.Users): password check
/// compatible with IFWEMS, lockout, TOTP MFA for privileged roles (SEC-002), server-side sessions with
/// inactivity timeout and revocation (SEC-009). Only users with an active TETA role may sign in.
/// </summary>
public sealed class AuthService : IAuthService
{
    private const string MfaPurpose = "mfa";
    private const string EnrolPurpose = "mfa-enrol";
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    private readonly ITetaDbContext _db;
    private readonly IPlatformPasswordHasher _hasher;
    private readonly IJwtTokenService _tokens;
    private readonly ITotpService _totp;
    private readonly IFieldProtector _protector;
    private readonly IClock _clock;
    private readonly ICurrentUser _current;
    private readonly ISettings _settings;
    private readonly IAuditWriter _audit;
    private readonly IMemoryCache _cache;

    public AuthService(ITetaDbContext db, IPlatformPasswordHasher hasher, IJwtTokenService tokens, ITotpService totp, IFieldProtector protector,
        IClock clock, ICurrentUser current, ISettings settings, IAuditWriter audit, IMemoryCache cache)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _totp = totp;
        _protector = protector;
        _clock = clock;
        _current = current;
        _settings = settings;
        _audit = audit;
        _cache = cache;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest r, string? ip, string? ua, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Username) || string.IsNullOrEmpty(r.Password)) return Fail("Enter your username and password.");
        var username = r.Username.Trim();
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == username || u.Email == username, ct);
        var now = _clock.UtcNow;

        if (user is null || !user.IsActive)
        {
            _audit.Write("Security", "Login", null, "LoginFailed", new { Username = username, Reason = "Unknown or inactive user", ip });
            await _db.SaveChangesAsync(ct);
            return Fail();
        }
        if (user.IsLockedOut(now))
        {
            _audit.Write("Security", "Login", user.Id.ToString(), "LoginBlockedLockedOut", new { user.Username, ip });
            await _db.SaveChangesAsync(ct);
            return Fail("The account is temporarily locked after repeated failed sign-ins. Try again later.");
        }

        var check = _hasher.Verify(user.PasswordHash, r.Password);
        if (check == PasswordCheck.Failed)
        {
            user.FailedLoginAttempts++;
            var max = Math.Max(3, await _settings.GetIntAsync(SettingKeys.SecurityMaxFailedLogins, ct));
            if (user.FailedLoginAttempts >= max)
            {
                user.LockedOutUntilUtc = now.AddMinutes(Math.Max(1, await _settings.GetIntAsync(SettingKeys.SecurityLockoutMinutes, ct)));
                _audit.Write("Security", "Login", user.Id.ToString(), "AccountLocked", new { user.Username, user.FailedLoginAttempts, ip });
            }
            _audit.Write("Security", "Login", user.Id.ToString(), "LoginFailed", new { user.Username, Reason = "Bad password", ip });
            await _db.SaveChangesAsync(ct);
            return Fail();
        }
        if (check == PasswordCheck.SuccessRehashNeeded) user.PasswordHash = _hasher.Hash(r.Password);
        user.FailedLoginAttempts = 0;
        user.LockedOutUntilUtc = null;

        var roles = await EffectiveRolesAsync(user.Id, ct);
        if (roles.Count == 0)
        {
            _audit.Write("Security", "Login", user.Id.ToString(), "LoginDenied", new { user.Username, Reason = "No TETA role" });
            await _db.SaveChangesAsync(ct);
            return Fail("Your account does not have access to TETA-IPPCMS. Contact the Security Administrator.");
        }

        var profile = await _db.UserSecurityProfiles.SingleOrDefaultAsync(p => p.UserId == user.Id, ct);
        var mfaRequired = await IsMfaRequiredAsync(roles, ct);
        if (profile?.MfaEnabled == true)
        {
            await _db.SaveChangesAsync(ct);
            return new LoginResult(true, null, null, Challenge(user.Id, MfaPurpose), true, false, "Enter the code from your authenticator app.", null);
        }
        if (mfaRequired)
        {
            await _db.SaveChangesAsync(ct);
            return new LoginResult(true, null, null, Challenge(user.Id, EnrolPurpose), false, true,
                "Your role requires multi-factor authentication. Set up an authenticator app to continue.", null);
        }
        return await IssueAsync(user, roles, mfa: false, ip, ua, ct);
    }

    public async Task<LoginResult> VerifyMfaAsync(MfaVerifyRequest r, string? ip, string? ua, CancellationToken ct)
    {
        var userId = ReadChallenge(r.ChallengeToken, MfaPurpose) ?? throw new ForbiddenException("The sign-in challenge has expired. Sign in again.");
        var user = await _db.Users.SingleAsync(u => u.Id == userId, ct);
        var profile = await _db.UserSecurityProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile?.MfaSecretProtected is null || !profile.MfaEnabled) throw new ForbiddenException("MFA is not set up for this account.");
        if (!_totp.Verify(_protector.Unprotect(profile.MfaSecretProtected), r.Code, _clock.UtcNow))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= Math.Max(3, await _settings.GetIntAsync(SettingKeys.SecurityMaxFailedLogins, ct)))
                user.LockedOutUntilUtc = _clock.UtcNow.AddMinutes(Math.Max(1, await _settings.GetIntAsync(SettingKeys.SecurityLockoutMinutes, ct)));
            _audit.Write("Security", "Login", userId.ToString(), "MfaFailed", new { user.Username, ip });
            await _db.SaveChangesAsync(ct);
            return Fail("The verification code is not valid.");
        }
        user.FailedLoginAttempts = 0;
        var roles = await EffectiveRolesAsync(userId, ct);
        return await IssueAsync(user, roles, mfa: true, ip, ua, ct);
    }

    public async Task<MfaEnrolmentDto> BeginMfaEnrolmentAsync(string? challengeToken, CancellationToken ct)
    {
        var userId = challengeToken is not null
            ? ReadChallenge(challengeToken, EnrolPurpose) ?? throw new ForbiddenException("The enrolment challenge has expired. Sign in again.")
            : _current.UserId ?? throw new ForbiddenException();
        var user = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, ct);
        var secret = _totp.GenerateSecret();
        var profile = await _db.UserSecurityProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            profile = new UserSecurityProfile { UserId = userId };
            _db.UserSecurityProfiles.Add(profile);
        }
        if (profile.MfaEnabled && challengeToken is not null) throw new ConflictException("MFA is already enabled; sign in with your code.");
        profile.MfaSecretProtected = _protector.Protect(secret);
        profile.MfaEnabled = false;
        await _db.SaveChangesAsync(ct);
        return new MfaEnrolmentDto(secret, _totp.BuildOtpAuthUri("TETA-IPPCMS", user.Username, secret), Challenge(userId, EnrolPurpose));
    }

    public async Task<LoginResult> ConfirmMfaEnrolmentAsync(MfaConfirmRequest r, string? ip, string? ua, CancellationToken ct)
    {
        var userId = ReadChallenge(r.ChallengeToken, EnrolPurpose) ?? throw new ForbiddenException("The enrolment challenge has expired. Sign in again.");
        var profile = await _db.UserSecurityProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct)
                      ?? throw new DomainException("Start MFA enrolment first.", "SEC-002");
        if (profile.MfaSecretProtected is null || !_totp.Verify(_protector.Unprotect(profile.MfaSecretProtected), r.Code, _clock.UtcNow))
            return Fail("The verification code is not valid. Check the time on your device and try again.");
        profile.MfaEnabled = true;
        profile.MfaEnrolledAtUtc = _clock.UtcNow;
        var user = await _db.Users.SingleAsync(u => u.Id == userId, ct);
        user.MfaEnabled = true;
        _audit.Write("Security", "Mfa", userId.ToString(), "MfaEnrolled", new { user.Username });
        var roles = await EffectiveRolesAsync(userId, ct);
        return await IssueAsync(user, roles, mfa: true, ip, ua, ct);
    }

    public async Task<LoginResult> RefreshAsync(CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new ForbiddenException();
        var sessionId = _current.SessionId ?? throw new ForbiddenException();
        if (!Guid.TryParse(sessionId, out var sid)) throw new ForbiddenException();
        var session = await _db.UserSessions.SingleOrDefaultAsync(s => s.Id == sid && s.UserId == userId, ct) ?? throw new ForbiddenException("Session not found.");
        var inactivity = TimeSpan.FromMinutes(await InactivityMinutesAsync(ct));
        if (!session.IsActive(_clock.UtcNow, inactivity)) throw new ForbiddenException("The session has expired. Sign in again.", "SEC-009");
        var user = await _db.Users.SingleAsync(u => u.Id == userId, ct);
        var roles = await EffectiveRolesAsync(userId, ct);
        if (roles.Count == 0 || !user.IsActive) throw new ForbiddenException("Access has been revoked.", "SEC-009");

        var (token, expires) = _tokens.CreateToken(new TokenRequest(user.Id, user.Username, user.DisplayName, roles, session.Id.ToString(), session.MfaSatisfied,
            await ExtraClaimsAsync(user.Id, ct)));
        session.ExpiresAtUtc = expires;
        session.LastSeenAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new LoginResult(true, token, expires, null, false, false, null, await MeAsync(ct));
    }

    public async Task LogoutAsync(CancellationToken ct)
    {
        if (_current.SessionId is { } sid && Guid.TryParse(sid, out var id))
        {
            var session = await _db.UserSessions.SingleOrDefaultAsync(s => s.Id == id, ct);
            if (session is not null && session.RevokedAtUtc is null)
            {
                session.RevokedAtUtc = _clock.UtcNow;
                session.RevokedReason = "Logout";
            }
            _audit.Write("Security", "Session", sid, "Logout", new { _current.Username });
            await _db.SaveChangesAsync(ct);
            _cache.Remove(SessionCacheKey(id));
        }
    }

    public async Task<CurrentUserDto> MeAsync(CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new ForbiddenException();
        var user = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, ct);
        var today = _clock.Today;
        var assignments = await _db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.Status == AssignmentStatus.Active && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .ToListAsync(ct);
        var roles = assignments.Select(a => a.RoleCode).Distinct().ToList();
        var permissions = await PermissionsForRolesAsync(roles, ct);
        var profile = await _db.UserSecurityProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
        var supplierId = await _db.SupplierUsers.AsNoTracking().Where(s => s.UserId == userId).Select(s => (Guid?)s.SupplierId).FirstOrDefaultAsync(ct);
        return new CurrentUserDto(user.Id, user.Username, user.DisplayName, user.Email, roles, permissions,
            assignments.Select(a => new RoleScopeDto(a.RoleCode, a.ScopeType.ToString(), a.ScopeId, a.ScopeName)).ToList(), supplierId,
            profile?.MfaEnabled == true, await IsMfaRequiredAsync(roles, ct), await InactivityMinutesAsync(ct));
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest r, CancellationToken ct)
    {
        var userId = _current.UserId ?? throw new ForbiddenException();
        var user = await _db.Users.SingleAsync(u => u.Id == userId, ct);
        if (_hasher.Verify(user.PasswordHash, r.CurrentPassword) == PasswordCheck.Failed)
            throw new ValidationException("currentPassword", "The current password is incorrect.");
        var errors = PasswordPolicy.Validate(r.NewPassword);
        if (errors.Count > 0) throw new ValidationException(new Dictionary<string, string[]> { ["newPassword"] = errors.ToArray() });
        user.PasswordHash = _hasher.Hash(r.NewPassword);
        user.ModifiedAtUtc = _clock.UtcNow;
        user.ModifiedBy = user.Username;

        // Changing the password ends every other session (SEC-009).
        var current = Guid.TryParse(_current.SessionId, out var sid) ? sid : Guid.Empty;
        foreach (var s in await _db.UserSessions.Where(s => s.UserId == userId && s.RevokedAtUtc == null && s.Id != current).ToListAsync(ct))
        {
            s.RevokedAtUtc = _clock.UtcNow;
            s.RevokedReason = "Password changed";
            _cache.Remove(SessionCacheKey(s.Id));
        }
        _audit.Write("Security", "User", userId.ToString(), "PasswordChanged", new { user.Username });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> PermissionsForRolesAsync(IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        if (roles.Count == 0) return Array.Empty<string>();
        var map = await _cache.GetOrCreateAsync("teta.rolepermissions", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            var rows = await _db.RolePermissions.AsNoTracking().Select(rp => new { rp.RoleCode, rp.Permission }).ToListAsync(ct);
            return rows.GroupBy(r => r.RoleCode).ToDictionary(g => g.Key, g => g.Select(x => x.Permission).ToArray());
        }) ?? new Dictionary<string, string[]>();
        return roles.SelectMany(r => map.TryGetValue(r, out var p) ? p : Array.Empty<string>()).Distinct().OrderBy(p => p).ToList();
    }

    /// <summary>Called on every authenticated request: the session must exist, be unrevoked and not idle (SEC-009).</summary>
    public async Task<bool> ValidateSessionAsync(Guid userId, string sessionId, CancellationToken ct)
    {
        if (!Guid.TryParse(sessionId, out var sid)) return false;
        var now = _clock.UtcNow;
        var inactivity = TimeSpan.FromMinutes(await InactivityMinutesAsync(ct));
        var key = SessionCacheKey(sid);
        if (_cache.TryGetValue(key, out DateTime lastTouched) && now - lastTouched < TimeSpan.FromSeconds(60)) return true;

        var session = await _db.UserSessions.SingleOrDefaultAsync(s => s.Id == sid && s.UserId == userId, ct);
        if (session is null || !session.IsActive(now, inactivity))
        {
            _cache.Remove(key);
            return false;
        }
        session.LastSeenAtUtc = now;
        await _db.SaveChangesAsync(ct);
        _cache.Set(key, now, TimeSpan.FromMinutes(2));
        return true;
    }

    // ----- helpers -----
    private async Task<LoginResult> IssueAsync(PlatformUser user, IReadOnlyList<string> roles, bool mfa, string? ip, string? ua, CancellationToken ct)
    {
        var session = new UserSession
        {
            UserId = user.Id, IssuedAtUtc = _clock.UtcNow, LastSeenAtUtc = _clock.UtcNow, IpAddress = ip,
            UserAgent = ua is { Length: > 400 } ? ua[..400] : ua, MfaSatisfied = mfa
        };
        var (token, expires) = _tokens.CreateToken(new TokenRequest(user.Id, user.Username, user.DisplayName, roles, session.Id.ToString(), mfa,
            await ExtraClaimsAsync(user.Id, ct)));
        session.ExpiresAtUtc = expires;
        _db.UserSessions.Add(session);

        var profile = await _db.UserSecurityProfiles.SingleOrDefaultAsync(p => p.UserId == user.Id, ct);
        if (profile is null)
        {
            profile = new UserSecurityProfile { UserId = user.Id };
            _db.UserSecurityProfiles.Add(profile);
        }
        profile.LastLoginAtUtc = _clock.UtcNow;
        _audit.Write("Security", "Login", user.Id.ToString(), "LoginSucceeded", new { user.Username, Mfa = mfa, ip, SessionId = session.Id });
        await _db.SaveChangesAsync(ct);

        var dto = new CurrentUserDto(user.Id, user.Username, user.DisplayName, user.Email, roles, await PermissionsForRolesAsync(roles, ct),
            Array.Empty<RoleScopeDto>(), await _db.SupplierUsers.Where(s => s.UserId == user.Id).Select(s => (Guid?)s.SupplierId).FirstOrDefaultAsync(ct),
            profile.MfaEnabled, await IsMfaRequiredAsync(roles, ct), await InactivityMinutesAsync(ct));
        return new LoginResult(true, token, expires, null, false, false, null, dto);
    }

    private async Task<IDictionary<string, string>?> ExtraClaimsAsync(Guid userId, CancellationToken ct)
    {
        var supplierId = await _db.SupplierUsers.AsNoTracking().Where(s => s.UserId == userId).Select(s => (Guid?)s.SupplierId).FirstOrDefaultAsync(ct);
        return supplierId is null ? null : new Dictionary<string, string> { [PlatformClaims.SupplierId] = supplierId.Value.ToString() };
    }

    private async Task<IReadOnlyList<string>> EffectiveRolesAsync(Guid userId, CancellationToken ct)
    {
        var today = _clock.Today;
        return await _db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.Status == AssignmentStatus.Active && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .Select(a => a.RoleCode).Distinct().ToListAsync(ct);
    }

    private async Task<bool> IsMfaRequiredAsync(IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        var required = await _settings.GetListAsync(SettingKeys.SecurityMfaRequiredRoles, ct);
        if (roles.Any(r => required.Contains(r, StringComparer.OrdinalIgnoreCase))) return true;
        var privileged = await _db.Roles.AsNoTracking().Where(r => r.IsPrivileged).Select(r => r.Code).ToListAsync(ct);
        return roles.Any(r => privileged.Contains(r));
    }

    private async Task<int> InactivityMinutesAsync(CancellationToken ct) =>
        Math.Max(5, await _settings.GetIntAsync(SettingKeys.SecurityInactivityMinutes, ct));

    private string Challenge(Guid userId, string purpose) =>
        _protector.Protect($"{purpose}|{userId}|{_clock.UtcNow.Add(ChallengeLifetime).Ticks.ToString(CultureInfo.InvariantCulture)}");

    private Guid? ReadChallenge(string token, string purpose)
    {
        try
        {
            var parts = _protector.Unprotect(token).Split('|');
            if (parts.Length != 3 || parts[0] != purpose) return null;
            if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks) || new DateTime(ticks, DateTimeKind.Utc) < _clock.UtcNow)
                return null;
            return Guid.TryParse(parts[1], out var id) ? id : null;
        }
        catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException or ArgumentException)
        {
            return null;
        }
    }

    private static string SessionCacheKey(Guid id) => $"teta.session.{id}";

    private static LoginResult Fail(string message = "The username or password is incorrect.") =>
        new(false, null, null, null, false, false, message, null);
}
