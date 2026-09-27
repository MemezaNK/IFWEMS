using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Admin;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.Application.Admin;

// ---------- DTOs ----------
public sealed record StepDto(Guid Id, int StepOrder, string Code, string Name, string RequiredRole, string? AuthorityType, decimal? MinimumValue, decimal? MaximumValue,
    int SlaHours, string? EscalationRole);
public sealed record WorkflowDefinitionDto(Guid Id, string Code, string Name, string EntityType, int DefinitionVersion, string Status, string? Description,
    DateTime? ActivatedAtUtc, string? ActivatedBy, string? CreatedBy, IReadOnlyList<StepDto> Steps, long Version);
public sealed record StepInput(int StepOrder, string Code, string Name, string RequiredRole, string? AuthorityType, decimal? MinimumValue, decimal? MaximumValue, int SlaHours,
    string? EscalationRole);
public sealed record SaveWorkflowDefinitionRequest(string Code, string Name, string EntityType, string? Description, IReadOnlyList<StepInput> Steps);

public sealed record DelegationDto(Guid Id, string AuthorityType, string? RoleCode, Guid? UserId, string? UserName, decimal MaxAmount, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, bool IsActive, string? PolicyReference, bool IsEffective, long Version);
public sealed record SaveDelegationRequest(string AuthorityType, string? RoleCode, Guid? UserId, decimal MaxAmount, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    bool IsActive, string? PolicyReference);

public sealed record SubstitutionDto(Guid Id, Guid PrincipalUserId, string PrincipalName, Guid SubstituteUserId, string SubstituteName, DateTime FromUtc,
    DateTime ToUtc, string Reason, bool IsRevoked, bool IsActive, string? CreatedBy, DateTime CreatedAtUtc);
public sealed record SaveSubstitutionRequest(Guid? PrincipalUserId, Guid SubstituteUserId, DateTime FromUtc, DateTime ToUtc, string Reason);

public sealed record SodRuleDto(Guid Id, string Code, string Description, string EntityType, string FirstAction, string SecondAction, string Mode, bool IsActive);
public sealed record SaveSodRuleRequest(string Code, string Description, string EntityType, string FirstAction, string SecondAction, SodMode Mode, bool IsActive);

public sealed record ReferenceItemDto(Guid Id, string Category, string Code, string Name, string? Description, bool IsActive, int SortOrder);
public sealed record SaveReferenceItemRequest(string Category, string Code, string Name, string? Description, bool IsActive, int SortOrder);

public sealed record HolidayDto(Guid Id, DateOnly Date, string Name);
public sealed record SaveHolidayRequest(DateOnly Date, string Name);

public sealed record SettingDto(Guid? Id, string Key, string Value, string? Description, string Category, bool IsDefault);
public sealed record SaveSettingRequest(string Value);

public sealed record TemplateDto(Guid Id, string Code, string Subject, string Body, bool SendEmail, bool IsActive);
public sealed record SaveTemplateRequest(string Subject, string Body, bool SendEmail, bool IsActive);

public sealed record RetentionDto(Guid Id, string RecordClass, int RetentionYears, string DisposalAction, string? LegalReference, bool IsActive);
public sealed record SaveRetentionRequest(string RecordClass, int RetentionYears, string DisposalAction, string? LegalReference, bool IsActive);

public sealed record NotificationDto(Guid Id, string Title, string Message, string? Link, string? Category, bool IsRead, DateTime CreatedAtUtc, string? EmailStatus);

public interface IAdminService
{
    Task<IReadOnlyList<WorkflowDefinitionDto>> ListWorkflowsAsync(CancellationToken ct);
    Task<WorkflowDefinitionDto> SaveWorkflowAsync(Guid? id, SaveWorkflowDefinitionRequest request, CancellationToken ct);
    Task<WorkflowDefinitionDto> SubmitWorkflowAsync(Guid id, CancellationToken ct);
    Task<WorkflowDefinitionDto> ActivateWorkflowAsync(Guid id, bool approve, string? comment, CancellationToken ct);

    Task<IReadOnlyList<DelegationDto>> ListDelegationsAsync(CancellationToken ct);
    Task<DelegationDto> SaveDelegationAsync(Guid? id, SaveDelegationRequest request, CancellationToken ct);
    Task<decimal?> MyLimitAsync(string authorityType, CancellationToken ct);

    Task<IReadOnlyList<SubstitutionDto>> ListSubstitutionsAsync(bool mineOnly, CancellationToken ct);
    Task<SubstitutionDto> SaveSubstitutionAsync(SaveSubstitutionRequest request, CancellationToken ct);
    Task<SubstitutionDto> RevokeSubstitutionAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<SodRuleDto>> ListSodRulesAsync(CancellationToken ct);
    Task<SodRuleDto> SaveSodRuleAsync(Guid? id, SaveSodRuleRequest request, CancellationToken ct);

    Task<IReadOnlyList<ReferenceItemDto>> ListReferenceDataAsync(string? category, bool activeOnly, CancellationToken ct);
    Task<ReferenceItemDto> SaveReferenceItemAsync(Guid? id, SaveReferenceItemRequest request, CancellationToken ct);

    Task<IReadOnlyList<HolidayDto>> ListHolidaysAsync(int? year, CancellationToken ct);
    Task<HolidayDto> SaveHolidayAsync(Guid? id, SaveHolidayRequest request, CancellationToken ct);
    Task DeleteHolidayAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<SettingDto>> ListSettingsAsync(CancellationToken ct);
    Task<SettingDto> SaveSettingAsync(string key, SaveSettingRequest request, CancellationToken ct);

    Task<IReadOnlyList<TemplateDto>> ListTemplatesAsync(CancellationToken ct);
    Task<TemplateDto> SaveTemplateAsync(string code, SaveTemplateRequest request, CancellationToken ct);

    Task<IReadOnlyList<RetentionDto>> ListRetentionAsync(CancellationToken ct);
    Task<RetentionDto> SaveRetentionAsync(Guid? id, SaveRetentionRequest request, CancellationToken ct);

    Task<PagedResult<NotificationDto>> MyNotificationsAsync(bool unreadOnly, int page, int pageSize, CancellationToken ct);
    Task<int> UnreadCountAsync(CancellationToken ct);
    Task MarkReadAsync(Guid? id, CancellationToken ct);
}

/// <summary>Workflow, delegation and administration configuration (SRS §5.11). All changes are audited with old/new values (FR-ADM-008).</summary>
public sealed class AdminService : IAdminService
{
    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly ISettings _settings;
    private readonly IUserRoles _roles;
    private readonly IDelegationService _delegations;
    private readonly IAuditWriter _audit;

    public AdminService(ITetaDbContext db, ICurrentUser user, IClock clock, ISettings settings, IUserRoles roles, IDelegationService delegations, IAuditWriter audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _settings = settings;
        _roles = roles;
        _delegations = delegations;
        _audit = audit;
    }

    // ----- Workflow designer (FR-ADM-001) -----
    public async Task<IReadOnlyList<WorkflowDefinitionDto>> ListWorkflowsAsync(CancellationToken ct) =>
        (await _db.WorkflowDefinitions.AsNoTracking().Include(d => d.Steps).OrderBy(d => d.Code).ThenByDescending(d => d.DefinitionVersion).ToListAsync(ct))
        .Select(ToDto).ToList();

    public async Task<WorkflowDefinitionDto> SaveWorkflowAsync(Guid? id, SaveWorkflowDefinitionRequest r, CancellationToken ct)
    {
        var roleCodes = await _db.Roles.Select(x => x.Code).ToListAsync(ct);
        var v = new Validator().Required("code", r.Code, 60).Required("name", r.Name, 200).Required("entityType", r.EntityType, 80)
            .Must(r.Steps is { Count: > 0 }, "steps", "Add at least one approval step.")
            .Must(r.Steps is null || r.Steps.Select(s => s.StepOrder).Distinct().Count() == r.Steps.Count, "steps", "Step order numbers must be unique.");
        foreach (var s in r.Steps ?? Array.Empty<StepInput>())
        {
            v.Required("steps.code", s.Code, 60).Required("steps.name", s.Name, 200).OneOf("steps.requiredRole", s.RequiredRole, roleCodes)
             .Range("steps.slaHours", s.SlaHours, 1, 24 * 60).NonNegative("steps.minimumValue", s.MinimumValue)
             .Must(s.MaximumValue is null || s.MinimumValue is null || s.MaximumValue >= s.MinimumValue, "steps.maximumValue", "Maximum value must be at least the minimum value.");
            if (s.AuthorityType is not null) v.OneOf("steps.authorityType", s.AuthorityType, AuthorityTypes.All);
            if (s.EscalationRole is not null) v.OneOf("steps.escalationRole", s.EscalationRole, roleCodes);
        }
        v.ThrowIfInvalid();

        WorkflowDefinition def;
        if (id is null)
        {
            var latest = await _db.WorkflowDefinitions.Where(d => d.Code == r.Code).MaxAsync(d => (int?)d.DefinitionVersion, ct) ?? 0;
            def = new WorkflowDefinition { Code = r.Code, DefinitionVersion = latest + 1 };
            _db.WorkflowDefinitions.Add(def);
        }
        else
        {
            def = await _db.WorkflowDefinitions.Include(d => d.Steps).SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Workflow", id);
            if (def.Status != ConfigStatus.Draft)
            {
                // Approved definitions are immutable; editing creates the next version (running instances keep theirs).
                var latest = await _db.WorkflowDefinitions.Where(d => d.Code == def.Code).MaxAsync(d => d.DefinitionVersion, ct);
                def = new WorkflowDefinition { Code = def.Code, DefinitionVersion = latest + 1 };
                _db.WorkflowDefinitions.Add(def);
            }
            else
            {
                foreach (var step in def.Steps.ToList()) _db.WorkflowStepDefinitions.Remove(step);
                def.Steps.Clear();
            }
        }
        def.Name = r.Name;
        def.EntityType = r.EntityType;
        def.Description = r.Description;
        foreach (var s in r.Steps!.OrderBy(s => s.StepOrder))
        {
            var step = new WorkflowStepDefinition
            {
                DefinitionId = def.Id, StepOrder = s.StepOrder, Code = s.Code, Name = s.Name, RequiredRole = s.RequiredRole, AuthorityType = s.AuthorityType,
                MinimumValue = s.MinimumValue, MaximumValue = s.MaximumValue, SlaHours = s.SlaHours, EscalationRole = s.EscalationRole
            };
            def.Steps.Add(step);
            _db.WorkflowStepDefinitions.Add(step);
        }
        await _db.SaveChangesAsync(ct);
        return ToDto(def);
    }

    public async Task<WorkflowDefinitionDto> SubmitWorkflowAsync(Guid id, CancellationToken ct)
    {
        var def = await _db.WorkflowDefinitions.Include(d => d.Steps).SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Workflow", id);
        if (def.Status != ConfigStatus.Draft) throw new DomainException("Only draft definitions can be submitted.", "FR-ADM-001");
        def.Status = ConfigStatus.PendingApproval;
        await _db.SaveChangesAsync(ct);
        return ToDto(def);
    }

    public async Task<WorkflowDefinitionDto> ActivateWorkflowAsync(Guid id, bool approve, string? comment, CancellationToken ct)
    {
        var def = await _db.WorkflowDefinitions.Include(d => d.Steps).SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Workflow", id);
        if (def.Status != ConfigStatus.PendingApproval) throw new DomainException("The definition is not awaiting approval.", "FR-ADM-001");
        if (def.CreatedByUserId == _user.UserId || def.UpdatedBy == _user.Username)
            throw new DomainException("Configuration changes need approval by a different authorised user.", "FR-ADM-008");
        if (!approve && string.IsNullOrWhiteSpace(comment)) throw new ValidationException("comment", "A reason is required when rejecting.");
        if (approve)
        {
            foreach (var older in await _db.WorkflowDefinitions.Where(d => d.Code == def.Code && d.Id != def.Id && d.Status == ConfigStatus.Approved).ToListAsync(ct))
                older.Status = ConfigStatus.Retired;
            def.Status = ConfigStatus.Approved;
            def.ActivatedAtUtc = _clock.UtcNow;
            def.ActivatedBy = _user.DisplayName ?? _user.Username;
        }
        else
        {
            def.Status = ConfigStatus.Draft;
        }
        _audit.Write("Admin", nameof(WorkflowDefinition), def.Id.ToString(), approve ? "WorkflowActivated" : "WorkflowRejected",
            new { def.Code, def.DefinitionVersion, Steps = def.Steps.OrderBy(s => s.StepOrder).Select(s => new { s.Code, s.RequiredRole, s.AuthorityType, s.MinimumValue, s.MaximumValue }) },
            comment);
        await _db.SaveChangesAsync(ct);
        return ToDto(def);
    }

    // ----- Delegations (FR-ADM-002) -----
    public async Task<IReadOnlyList<DelegationDto>> ListDelegationsAsync(CancellationToken ct)
    {
        var today = _clock.Today;
        return (await _db.Delegations.AsNoTracking().OrderBy(d => d.AuthorityType).ThenByDescending(d => d.MaxAmount).ToListAsync(ct))
            .Select(d => new DelegationDto(d.Id, d.AuthorityType, d.RoleCode, d.UserId, d.UserName, d.MaxAmount, d.EffectiveFrom, d.EffectiveTo, d.IsActive,
                d.PolicyReference, d.IsEffectiveOn(today), d.Version)).ToList();
    }

    public async Task<DelegationDto> SaveDelegationAsync(Guid? id, SaveDelegationRequest r, CancellationToken ct)
    {
        new Validator().OneOf("authorityType", r.AuthorityType, AuthorityTypes.All).NonNegative("maxAmount", r.MaxAmount)
            .Must((r.RoleCode is null) != (r.UserId is null), "roleCode", "Delegate to either a role or a named user.")
            .DateOrder("effectiveFrom", r.EffectiveFrom, "effectiveTo", r.EffectiveTo).ThrowIfInvalid();
        string? userName = null;
        if (r.UserId is { } uid)
        {
            userName = await _db.Users.Where(u => u.Id == uid).Select(u => u.DisplayName).SingleOrDefaultAsync(ct) ?? throw new ValidationException("userId", "User not found.");
            if (uid == _user.UserId) throw new DomainException("You cannot grant a delegation to yourself.", "SEC-005");
        }
        if (r.RoleCode is not null && !await _db.Roles.AnyAsync(x => x.Code == r.RoleCode, ct)) throw new ValidationException("roleCode", "Unknown role.");

        Delegation d;
        if (id is null)
        {
            d = new Delegation();
            _db.Delegations.Add(d);
        }
        else
        {
            d = await _db.Delegations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Delegation", id);
        }
        d.AuthorityType = r.AuthorityType;
        d.RoleCode = r.RoleCode;
        d.UserId = r.UserId;
        d.UserName = userName;
        d.MaxAmount = r.MaxAmount;
        d.EffectiveFrom = r.EffectiveFrom;
        d.EffectiveTo = r.EffectiveTo;
        d.IsActive = r.IsActive;
        d.PolicyReference = r.PolicyReference;
        await _db.SaveChangesAsync(ct);
        return (await ListDelegationsAsync(ct)).Single(x => x.Id == d.Id);
    }

    public async Task<decimal?> MyLimitAsync(string authorityType, CancellationToken ct)
    {
        var userId = _user.UserId ?? throw new ForbiddenException();
        return await _delegations.GetLimitAsync(userId, await _roles.GetEffectiveRolesAsync(userId, ct), authorityType, ct);
    }

    // ----- Substitutes (FR-ADM-003) -----
    public async Task<IReadOnlyList<SubstitutionDto>> ListSubstitutionsAsync(bool mineOnly, CancellationToken ct)
    {
        var q = _db.Substitutions.AsNoTracking();
        if (mineOnly && _user.UserId is { } me) q = q.Where(s => s.PrincipalUserId == me || s.SubstituteUserId == me);
        var now = _clock.UtcNow;
        return (await q.OrderByDescending(s => s.FromUtc).ToListAsync(ct)).Select(s => ToDto(s, now)).ToList();
    }

    public async Task<SubstitutionDto> SaveSubstitutionAsync(SaveSubstitutionRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("substituteUserId", r.SubstituteUserId).Required("reason", r.Reason, 500)
            .Must(r.ToUtc > r.FromUtc, "toUtc", "The end must be after the start.").Must(r.ToUtc - r.FromUtc <= TimeSpan.FromDays(90), "toUtc", "Substitutions are limited to 90 days.")
            .ThrowIfInvalid();
        var principalId = r.PrincipalUserId ?? _user.UserId ?? throw new ForbiddenException();
        if (principalId != _user.UserId && !_user.HasPermission(Permissions.AdminDelegations))
            throw new ForbiddenException("Only administrators can appoint substitutes for other users.");
        if (principalId == r.SubstituteUserId) throw new ValidationException("substituteUserId", "Choose someone other than the principal.");
        var names = await _db.Users.Where(u => u.Id == principalId || u.Id == r.SubstituteUserId).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        if (!names.ContainsKey(principalId) || !names.ContainsKey(r.SubstituteUserId)) throw new ValidationException("substituteUserId", "User not found.");
        var s = new Substitution
        {
            PrincipalUserId = principalId, PrincipalName = names[principalId], SubstituteUserId = r.SubstituteUserId, SubstituteName = names[r.SubstituteUserId],
            FromUtc = DateTime.SpecifyKind(r.FromUtc, DateTimeKind.Utc), ToUtc = DateTime.SpecifyKind(r.ToUtc, DateTimeKind.Utc), Reason = r.Reason
        };
        _db.Substitutions.Add(s);
        await _db.SaveChangesAsync(ct);
        return ToDto(s, _clock.UtcNow);
    }

    public async Task<SubstitutionDto> RevokeSubstitutionAsync(Guid id, CancellationToken ct)
    {
        var s = await _db.Substitutions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Substitution", id);
        if (s.PrincipalUserId != _user.UserId && !_user.HasPermission(Permissions.AdminDelegations)) throw new ForbiddenException();
        s.IsRevoked = true;
        await _db.SaveChangesAsync(ct);
        return ToDto(s, _clock.UtcNow);
    }

    // ----- SoD rules (SEC-005, BR-008) -----
    public async Task<IReadOnlyList<SodRuleDto>> ListSodRulesAsync(CancellationToken ct) =>
        await _db.SodRules.AsNoTracking().OrderBy(r => r.EntityType).ThenBy(r => r.Code)
            .Select(r => new SodRuleDto(r.Id, r.Code, r.Description, r.EntityType, r.FirstAction, r.SecondAction, r.Mode.ToString(), r.IsActive)).ToListAsync(ct);

    public async Task<SodRuleDto> SaveSodRuleAsync(Guid? id, SaveSodRuleRequest r, CancellationToken ct)
    {
        new Validator().Required("code", r.Code, 60).Required("description", r.Description, 500).Required("entityType", r.EntityType, 80)
            .Required("firstAction", r.FirstAction, 80).Required("secondAction", r.SecondAction, 80).ThrowIfInvalid();
        SodRule rule;
        if (id is null)
        {
            if (await _db.SodRules.AnyAsync(x => x.Code == r.Code, ct)) throw new ConflictException($"Rule {r.Code} already exists.");
            rule = new SodRule { Code = r.Code };
            _db.SodRules.Add(rule);
        }
        else
        {
            rule = await _db.SodRules.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("SoD rule", id);
        }
        rule.Description = r.Description;
        rule.EntityType = r.EntityType;
        rule.FirstAction = r.FirstAction;
        rule.SecondAction = r.SecondAction;
        rule.Mode = r.Mode;
        rule.IsActive = r.IsActive;
        await _db.SaveChangesAsync(ct);
        return new SodRuleDto(rule.Id, rule.Code, rule.Description, rule.EntityType, rule.FirstAction, rule.SecondAction, rule.Mode.ToString(), rule.IsActive);
    }

    // ----- Reference data (FR-ADM-006) -----
    public async Task<IReadOnlyList<ReferenceItemDto>> ListReferenceDataAsync(string? category, bool activeOnly, CancellationToken ct)
    {
        var q = _db.ReferenceData.AsNoTracking();
        if (!string.IsNullOrEmpty(category)) q = q.Where(x => x.Category == category);
        if (activeOnly) q = q.Where(x => x.IsActive);
        return await q.OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new ReferenceItemDto(x.Id, x.Category, x.Code, x.Name, x.Description, x.IsActive, x.SortOrder)).ToListAsync(ct);
    }

    public async Task<ReferenceItemDto> SaveReferenceItemAsync(Guid? id, SaveReferenceItemRequest r, CancellationToken ct)
    {
        new Validator().Required("category", r.Category, 60).Required("code", r.Code, 60).Required("name", r.Name, 200).ThrowIfInvalid();
        ReferenceDataItem item;
        if (id is null)
        {
            if (await _db.ReferenceData.AnyAsync(x => x.Category == r.Category && x.Code == r.Code, ct))
                throw new ConflictException($"{r.Category}/{r.Code} already exists.");
            item = new ReferenceDataItem { Category = r.Category, Code = r.Code };
            _db.ReferenceData.Add(item);
        }
        else
        {
            item = await _db.ReferenceData.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Reference item", id);
        }
        item.Name = r.Name;
        item.Description = r.Description;
        item.IsActive = r.IsActive;
        item.SortOrder = r.SortOrder;
        await _db.SaveChangesAsync(ct);
        return new ReferenceItemDto(item.Id, item.Category, item.Code, item.Name, item.Description, item.IsActive, item.SortOrder);
    }

    // ----- Business calendar (FR-ADM-007) -----
    public async Task<IReadOnlyList<HolidayDto>> ListHolidaysAsync(int? year, CancellationToken ct)
    {
        var q = _db.PublicHolidays.AsNoTracking();
        if (year is { } y) q = q.Where(h => h.Date >= new DateOnly(y, 1, 1) && h.Date <= new DateOnly(y, 12, 31));
        return await q.OrderBy(h => h.Date).Select(h => new HolidayDto(h.Id, h.Date, h.Name)).ToListAsync(ct);
    }

    public async Task<HolidayDto> SaveHolidayAsync(Guid? id, SaveHolidayRequest r, CancellationToken ct)
    {
        new Validator().Required("name", r.Name, 100).ThrowIfInvalid();
        PublicHoliday h;
        if (id is null)
        {
            if (await _db.PublicHolidays.AnyAsync(x => x.Date == r.Date, ct)) throw new ConflictException($"{r.Date:yyyy-MM-dd} is already a holiday.");
            h = new PublicHoliday();
            _db.PublicHolidays.Add(h);
        }
        else
        {
            h = await _db.PublicHolidays.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Holiday", id);
        }
        h.Date = r.Date;
        h.Name = r.Name;
        await _db.SaveChangesAsync(ct);
        return new HolidayDto(h.Id, h.Date, h.Name);
    }

    public async Task DeleteHolidayAsync(Guid id, CancellationToken ct)
    {
        var h = await _db.PublicHolidays.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Holiday", id);
        _db.PublicHolidays.Remove(h);
        await _db.SaveChangesAsync(ct);
    }

    // ----- Settings (NFR-011, FR-ADM-008) -----
    public async Task<IReadOnlyList<SettingDto>> ListSettingsAsync(CancellationToken ct)
    {
        var stored = await _db.SystemSettings.AsNoTracking().ToListAsync(ct);
        var keys = SettingKeys.Defaults.Keys.Union(stored.Select(s => s.Key)).OrderBy(k => k);
        return keys.Select(k =>
        {
            var s = stored.FirstOrDefault(x => x.Key == k);
            SettingKeys.Defaults.TryGetValue(k, out var def);
            return new SettingDto(s?.Id, k, s?.Value ?? def.Value ?? string.Empty, s?.Description ?? def.Description, s?.Category ?? def.Category ?? "General", s is null);
        }).ToList();
    }

    public async Task<SettingDto> SaveSettingAsync(string key, SaveSettingRequest r, CancellationToken ct)
    {
        new Validator().Required("value", r.Value, 2000).ThrowIfInvalid();
        var setting = await _db.SystemSettings.SingleOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            if (!SettingKeys.Defaults.TryGetValue(key, out var def)) throw new NotFoundException("Setting", key);
            setting = new SystemSetting { Key = key, Category = def.Category, Description = def.Description };
            _db.SystemSettings.Add(setting);
        }
        setting.Value = r.Value.Trim();
        await _db.SaveChangesAsync(ct);
        _settings.Invalidate();
        return new SettingDto(setting.Id, setting.Key, setting.Value, setting.Description, setting.Category, false);
    }

    // ----- Notification templates (FR-ADM-004) -----
    public async Task<IReadOnlyList<TemplateDto>> ListTemplatesAsync(CancellationToken ct)
    {
        var stored = await _db.NotificationTemplates.AsNoTracking().ToListAsync(ct);
        return stored.OrderBy(t => t.Code).Select(t => new TemplateDto(t.Id, t.Code, t.Subject, t.Body, t.SendEmail, t.IsActive)).ToList();
    }

    public async Task<TemplateDto> SaveTemplateAsync(string code, SaveTemplateRequest r, CancellationToken ct)
    {
        new Validator().Required("subject", r.Subject, 300).Required("body", r.Body, 20000).ThrowIfInvalid();
        var t = await _db.NotificationTemplates.SingleOrDefaultAsync(x => x.Code == code, ct);
        if (t is null)
        {
            t = new NotificationTemplate { Code = code };
            _db.NotificationTemplates.Add(t);
        }
        t.Subject = r.Subject;
        t.Body = r.Body;
        t.SendEmail = r.SendEmail;
        t.IsActive = r.IsActive;
        await _db.SaveChangesAsync(ct);
        return new TemplateDto(t.Id, t.Code, t.Subject, t.Body, t.SendEmail, t.IsActive);
    }

    // ----- Retention (NFR-013) -----
    public async Task<IReadOnlyList<RetentionDto>> ListRetentionAsync(CancellationToken ct) =>
        await _db.RetentionPolicies.AsNoTracking().OrderBy(r => r.RecordClass)
            .Select(r => new RetentionDto(r.Id, r.RecordClass, r.RetentionYears, r.DisposalAction, r.LegalReference, r.IsActive)).ToListAsync(ct);

    public async Task<RetentionDto> SaveRetentionAsync(Guid? id, SaveRetentionRequest r, CancellationToken ct)
    {
        new Validator().Required("recordClass", r.RecordClass, 100).Range("retentionYears", r.RetentionYears, 1, 100)
            .OneOf("disposalAction", r.DisposalAction, new[] { "Review", "Destroy", "Archive", "TransferToArchives" }).ThrowIfInvalid();
        RetentionPolicy p;
        if (id is null)
        {
            p = new RetentionPolicy { RecordClass = r.RecordClass };
            _db.RetentionPolicies.Add(p);
        }
        else
        {
            p = await _db.RetentionPolicies.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Retention policy", id);
        }
        p.RetentionYears = r.RetentionYears;
        p.DisposalAction = r.DisposalAction;
        p.LegalReference = r.LegalReference;
        p.IsActive = r.IsActive;
        await _db.SaveChangesAsync(ct);
        return new RetentionDto(p.Id, p.RecordClass, p.RetentionYears, p.DisposalAction, p.LegalReference, p.IsActive);
    }

    // ----- My notifications -----
    public async Task<PagedResult<NotificationDto>> MyNotificationsAsync(bool unreadOnly, int page, int pageSize, CancellationToken ct)
    {
        var userId = _user.UserId ?? throw new ForbiddenException();
        var q = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        var total = await q.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var items = await q.OrderByDescending(n => n.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.Link, n.Category, n.IsRead, n.CreatedAtUtc, n.EmailStatus)).ToListAsync(ct);
        return new PagedResult<NotificationDto>(items, total, page, pageSize);
    }

    public async Task<int> UnreadCountAsync(CancellationToken ct)
    {
        var userId = _user.UserId ?? throw new ForbiddenException();
        return await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    public async Task MarkReadAsync(Guid? id, CancellationToken ct)
    {
        var userId = _user.UserId ?? throw new ForbiddenException();
        var q = _db.Notifications.Where(n => n.UserId == userId && !n.IsRead);
        if (id is { } nid) q = q.Where(n => n.Id == nid);
        foreach (var n in await q.ToListAsync(ct))
        {
            n.IsRead = true;
            n.ReadAtUtc = _clock.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    private static WorkflowDefinitionDto ToDto(WorkflowDefinition d) =>
        new(d.Id, d.Code, d.Name, d.EntityType, d.DefinitionVersion, d.Status.ToString(), d.Description, d.ActivatedAtUtc, d.ActivatedBy, d.CreatedBy,
            d.Steps.OrderBy(s => s.StepOrder).Select(s => new StepDto(s.Id, s.StepOrder, s.Code, s.Name, s.RequiredRole, s.AuthorityType, s.MinimumValue, s.MaximumValue,
                s.SlaHours, s.EscalationRole)).ToList(), d.Version);

    private static SubstitutionDto ToDto(Substitution s, DateTime now) =>
        new(s.Id, s.PrincipalUserId, s.PrincipalName, s.SubstituteUserId, s.SubstituteName, s.FromUtc, s.ToUtc, s.Reason, s.IsRevoked, s.IsActiveAt(now),
            s.CreatedBy, s.CreatedAtUtc);
}
