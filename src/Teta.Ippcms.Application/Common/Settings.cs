using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Teta.Ippcms.Application.Abstractions;

namespace Teta.Ippcms.Application.Common;

/// <summary>Configurable business settings (NFR-011) with seeded defaults.</summary>
public static class SettingKeys
{
    public const string ContractExpiryLeadDays = "Contracts.ExpiryAlertLeadDays";
    public const string DocumentExpiryLeadDays = "Documents.ExpiryAlertLeadDays";
    public const string HealthScheduleAmberDays = "Health.Schedule.AmberDays";
    public const string HealthScheduleRedDays = "Health.Schedule.RedDays";
    public const string HealthCostAmberPercent = "Health.Cost.AmberPercent";
    public const string HealthCostRedPercent = "Health.Cost.RedPercent";
    public const string HealthHighRisksAmber = "Health.Risk.HighRisksAmber";
    public const string HealthOverdueMilestonesAmber = "Health.Delivery.OverdueMilestonesAmber";
    public const string EscalationGraceDays = "Escalation.GraceDays";
    public const string EscalationRoleLevel1 = "Escalation.Level1Role";
    public const string EscalationRoleLevel2 = "Escalation.Level2Role";
    public const string ExceptionOverBudgetPercent = "Exceptions.OverBudgetPercent";
    public const string ExceptionProcurementAgeingDays = "Exceptions.ProcurementAgeingDays";
    public const string SecurityMfaRequiredRoles = "Security.MfaRequiredRoles";
    public const string SecurityInactivityMinutes = "Security.InactivityTimeoutMinutes";
    public const string SecurityMaxFailedLogins = "Security.MaxFailedLogins";
    public const string SecurityLockoutMinutes = "Security.LockoutMinutes";
    public const string DocumentMaxUploadMb = "Documents.MaxUploadMb";
    public const string DocumentAllowedExtensions = "Documents.AllowedExtensions";
    public const string GeoMinimumGroupSize = "Privacy.GeoMinimumGroupSize";
    public const string CurrentFinancialYear = "Planning.CurrentFinancialYear";

    public static readonly IReadOnlyDictionary<string, (string Value, string Category, string Description)> Defaults =
        new Dictionary<string, (string, string, string)>
        {
            [ContractExpiryLeadDays] = ("90,60,30", "Contracts", "Days before contract end date to notify the contract manager (FR-CON-006)."),
            [DocumentExpiryLeadDays] = ("60,30,7", "Contracts", "Days before supporting-document expiry to notify (FR-CON-006)."),
            [HealthScheduleAmberDays] = ("10", "Health", "Schedule variance (days) above which the schedule dimension is Amber (FR-EXE-011)."),
            [HealthScheduleRedDays] = ("30", "Health", "Schedule variance (days) above which the schedule dimension is Red."),
            [HealthCostAmberPercent] = ("5", "Health", "Forecast cost variance % above which cost is Amber."),
            [HealthCostRedPercent] = ("10", "Health", "Forecast cost variance % above which cost is Red."),
            [HealthHighRisksAmber] = ("2", "Health", "Number of open high risks above which risk is Amber."),
            [HealthOverdueMilestonesAmber] = ("2", "Health", "Number of overdue milestones above which delivery is Amber."),
            [EscalationGraceDays] = ("0", "Escalation", "Days after due date before an overdue item escalates (FR-ADM-005)."),
            [EscalationRoleLevel1] = ("HeadPMO", "Escalation", "Role notified at escalation level 1."),
            [EscalationRoleLevel2] = ("ExecutiveAuthority", "Escalation", "Role notified at escalation level 2."),
            [ExceptionOverBudgetPercent] = ("0", "Exceptions", "Percent over budget at which a project is reported as over budget (FR-REP-005)."),
            [ExceptionProcurementAgeingDays] = ("90", "Exceptions", "Days in progress after which a procurement is reported as aged (FR-REP-005, RPT-004)."),
            [SecurityMfaRequiredRoles] = ("ExecutiveAuthority,CFO,SystemAdministrator,SecurityAdministrator", "Security", "Roles that must use MFA (SEC-002)."),
            [SecurityInactivityMinutes] = ("20", "Security", "Minutes of inactivity after which a session expires (SEC-009)."),
            [SecurityMaxFailedLogins] = ("5", "Security", "Failed logins before temporary lockout."),
            [SecurityLockoutMinutes] = ("15", "Security", "Lockout duration in minutes."),
            [DocumentMaxUploadMb] = ("25", "Documents", "Maximum upload size in MB (SRS §36)."),
            [DocumentAllowedExtensions] = (".pdf,.docx,.xlsx,.pptx,.jpg,.jpeg,.png,.csv,.txt,.zip", "Documents", "Permitted upload file types (SRS §36)."),
            [GeoMinimumGroupSize] = ("5", "Privacy", "Minimum beneficiaries per area before counts are shown on the geographic view (FR-REP-009)."),
            [CurrentFinancialYear] = ("2026/27", "Planning", "Current financial year used as default in planning screens.")
        };
}

public interface ISettings
{
    Task<string> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default);
    Task<decimal> GetDecimalAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetIntListAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetListAsync(string key, CancellationToken cancellationToken = default);
    void Invalidate();
}

public sealed class SettingsService : ISettings
{
    private const string CacheKey = "teta.settings";
    private readonly ITetaDbContext _db;
    private readonly IMemoryCache _cache;

    public SettingsService(ITetaDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<string> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var all = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return await _db.SystemSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);
        }) ?? new Dictionary<string, string>();

        if (all.TryGetValue(key, out var value)) return value;
        return SettingKeys.Defaults.TryGetValue(key, out var def) ? def.Value : string.Empty;
    }

    public async Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) =>
        int.TryParse(await GetAsync(key, cancellationToken), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    public async Task<decimal> GetDecimalAsync(string key, CancellationToken cancellationToken = default) =>
        decimal.TryParse(await GetAsync(key, cancellationToken), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;

    public async Task<IReadOnlyList<int>> GetIntListAsync(string key, CancellationToken cancellationToken = default) =>
        (await GetListAsync(key, cancellationToken))
            .Select(s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (int?)null)
            .Where(v => v.HasValue).Select(v => v!.Value).ToList();

    public async Task<IReadOnlyList<string>> GetListAsync(string key, CancellationToken cancellationToken = default) =>
        (await GetAsync(key, cancellationToken)).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Invalidate() => _cache.Remove(CacheKey);
}
