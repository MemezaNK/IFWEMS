using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Platform.Security.Crypto;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Documents;
using Teta.Ippcms.Domain.Monitoring;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Application.Monitoring;

// ---------- DTOs ----------
public sealed record MePlanDto(Guid Id, Guid ProjectId, string Description, string Frequency, string Methods, string? Indicators,
    Guid? ResponsibleOfficerUserId, string ResponsibleOfficerName, DateOnly? NextVisitDue, long Version);
public sealed record SaveMePlanRequest(string Description, string Frequency, string Methods, string? Indicators, Guid? ResponsibleOfficerUserId,
    string ResponsibleOfficerName, DateOnly? NextVisitDue);

public sealed record TemplateField(string Key, string Label, string Type, bool Required, IReadOnlyList<string>? Options);
public sealed record TemplateDto(Guid Id, string Code, string Name, string? ProjectType, int TemplateVersion, string Status,
    IReadOnlyList<TemplateField> Fields, DateTime? PublishedAtUtc, long Version);
public sealed record SaveTemplateRequest(string Code, string Name, string? ProjectType, IReadOnlyList<TemplateField> Fields);

public sealed record VisitDto(Guid Id, string Number, Guid ProjectId, string ProjectReference, Guid? TemplateId, int? TemplateVersion,
    IReadOnlyList<TemplateField> Fields, string Type, DateOnly ScheduledDate, DateOnly? VisitDate, string? Officials, string? Location,
    decimal? Latitude, decimal? Longitude, IReadOnlyDictionary<string, string?> Responses, string? Summary, string? Outcome, string Status,
    Guid? ProviderSupplierId, int FindingCount, int EvidenceCount, long Version);
public sealed record ScheduleVisitRequest(Guid ProjectId, Guid? TemplateId, VisitType Type, DateOnly ScheduledDate, string? Officials, string? Location,
    Guid? ProviderSupplierId);
public sealed record CaptureVisitRequest(DateOnly VisitDate, string Officials, string? Location, decimal? Latitude, decimal? Longitude,
    Dictionary<string, string?> Responses, string Summary, string Outcome, bool Complete);

public sealed record FindingDto(Guid Id, string Number, Guid ProjectId, string ProjectReference, Guid? VisitId, string Source, string Description,
    string Severity, string? RootCause, string Status, int OpenActions, DateTime CreatedAtUtc, long Version);
public sealed record SaveFindingRequest(Guid ProjectId, Guid? VisitId, string Description, Severity Severity, string? RootCause);

public sealed record ActionDto(Guid Id, string Number, string ParentType, Guid ParentId, Guid? ProjectId, string Description, Guid? OwnerUserId,
    string OwnerName, DateOnly DueDate, string Status, string? ClosureNotes, DateTime? CompletedAtUtc, int EscalationLevel, bool IsOverdue,
    int EvidenceCount, long Version);
public sealed record SaveActionRequest(string ParentType, Guid ParentId, string Description, Guid? OwnerUserId, string OwnerName, DateOnly DueDate);
public sealed record CompleteActionRequest(string ClosureNotes);

public sealed record BeneficiaryDto(Guid Id, string Number, Guid ProjectId, string ProjectReference, string IdentifierMasked, string IdentifierType,
    string? FirstName, string? LastName, string? Gender, int? BirthYear, string? Province, string? District, string Intervention,
    Guid? ProviderSupplierId, string? FundingSource, string Status, bool PotentialDuplicate, string? DuplicateNote, bool ConsentObtained,
    bool PiiVisible, long Version);
public sealed record SaveBeneficiaryRequest(Guid ProjectId, string Identifier, string IdentifierType, string FirstName, string LastName, string? Gender,
    int? BirthYear, string? Province, string? District, string Intervention, Guid? ProviderSupplierId, string? FundingSource, bool ConsentObtained);
public sealed record BeneficiaryStatusRequest(BeneficiaryStatus Status, string? Note);
public sealed record BeneficiaryHistoryDto(string FromStatus, string ToStatus, DateTime ChangedAtUtc, string? ChangedBy, string? Note);

public sealed record MeDashboardDto(int VisitsScheduled, int VisitsCompleted, int VisitsOverdue, int OpenFindings, int CriticalFindings, int OverdueActions,
    int Beneficiaries, int Completions, int PotentialDuplicates, IReadOnlyList<NamedCount> FindingsBySeverity, IReadOnlyList<NamedCount> BeneficiariesByStatus,
    IReadOnlyList<TrendPoint> VisitTrend, IReadOnlyList<TrendPoint> OutcomeTrend, IReadOnlyList<ActionDto> OverdueActionList);
public sealed record NamedCount(string Name, int Count);
public sealed record TrendPoint(string Period, int Value);

public interface IMonitoringService
{
    Task<MePlanDto?> GetPlanAsync(Guid projectId, CancellationToken ct);
    Task<MePlanDto> SavePlanAsync(Guid projectId, SaveMePlanRequest request, CancellationToken ct);

    Task<IReadOnlyList<TemplateDto>> ListTemplatesAsync(bool publishedOnly, CancellationToken ct);
    Task<TemplateDto> SaveTemplateAsync(Guid? id, SaveTemplateRequest request, CancellationToken ct);
    Task<TemplateDto> PublishTemplateAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<VisitDto>> ListVisitsAsync(Guid? projectId, string? status, CancellationToken ct);
    Task<VisitDto> GetVisitAsync(Guid id, CancellationToken ct);
    Task<VisitDto> ScheduleVisitAsync(ScheduleVisitRequest request, CancellationToken ct);
    Task<VisitDto> CaptureVisitAsync(Guid id, CaptureVisitRequest request, CancellationToken ct);

    Task<IReadOnlyList<FindingDto>> ListFindingsAsync(Guid? projectId, bool openOnly, CancellationToken ct);
    Task<FindingDto> SaveFindingAsync(Guid? id, SaveFindingRequest request, CancellationToken ct);

    Task<IReadOnlyList<ActionDto>> ListActionsAsync(string? parentType, Guid? parentId, bool openOnly, bool mineOnly, CancellationToken ct);
    Task<ActionDto> SaveActionAsync(Guid? id, SaveActionRequest request, CancellationToken ct);
    Task<ActionDto> CompleteActionAsync(Guid id, CompleteActionRequest request, CancellationToken ct);

    Task<PagedResult<BeneficiaryDto>> ListBeneficiariesAsync(Guid? projectId, string? status, string? search, int page, int pageSize, CancellationToken ct);
    Task<BeneficiaryDto> RegisterBeneficiaryAsync(SaveBeneficiaryRequest request, CancellationToken ct);
    Task<BeneficiaryDto> ChangeBeneficiaryStatusAsync(Guid id, BeneficiaryStatusRequest request, CancellationToken ct);
    Task<IReadOnlyList<BeneficiaryHistoryDto>> BeneficiaryHistoryAsync(Guid id, CancellationToken ct);
    Task<string> RevealIdentifierAsync(Guid id, string reason, CancellationToken ct);
    Task<BeneficiaryDto> ResolveDuplicateAsync(Guid id, bool isDuplicate, string note, CancellationToken ct);

    Task<MeDashboardDto> DashboardAsync(Guid? programmeId, Guid? projectId, Guid? providerId, CancellationToken ct);
}

/// <summary>Monitoring, evaluation and beneficiaries (SRS §5.8) with POPIA controls (SEC-011).</summary>
public sealed class MonitoringService : IMonitoringService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] FieldTypes = { "text", "textarea", "number", "date", "select", "yesno", "rating" };

    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAccessScope _scope;
    private readonly INumberGenerator _numbers;
    private readonly IFieldProtector _protector;
    private readonly INotifier _notifier;
    private readonly IAuditWriter _audit;

    public MonitoringService(ITetaDbContext db, ICurrentUser user, IClock clock, IAccessScope scope, INumberGenerator numbers, IFieldProtector protector,
        INotifier notifier, IAuditWriter audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _scope = scope;
        _numbers = numbers;
        _protector = protector;
        _notifier = notifier;
        _audit = audit;
    }

    // ----- M&E plan (FR-ME-001) -----
    public async Task<MePlanDto?> GetPlanAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        var p = await _db.MePlans.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == projectId, ct);
        return p is null ? null : ToDto(p);
    }

    public async Task<MePlanDto> SavePlanAsync(Guid projectId, SaveMePlanRequest r, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        new Validator().Required("description", r.Description, 4000).Required("frequency", r.Frequency, 50).Required("methods", r.Methods, 2000)
            .Required("responsibleOfficerName", r.ResponsibleOfficerName, 200).ThrowIfInvalid();
        var plan = await _db.MePlans.SingleOrDefaultAsync(x => x.ProjectId == projectId, ct);
        if (plan is null)
        {
            plan = new MePlan { ProjectId = projectId };
            _db.MePlans.Add(plan);
        }
        plan.Description = r.Description;
        plan.Frequency = r.Frequency;
        plan.Methods = r.Methods;
        plan.Indicators = r.Indicators;
        plan.ResponsibleOfficerUserId = r.ResponsibleOfficerUserId;
        plan.ResponsibleOfficerName = r.ResponsibleOfficerName;
        plan.NextVisitDue = r.NextVisitDue;
        await _db.SaveChangesAsync(ct);
        return ToDto(plan);
    }

    // ----- Templates (FR-ME-002) -----
    public async Task<IReadOnlyList<TemplateDto>> ListTemplatesAsync(bool publishedOnly, CancellationToken ct)
    {
        var q = _db.MonitoringTemplates.AsNoTracking();
        if (publishedOnly) q = q.Where(t => t.Status == TemplateStatus.Published);
        return (await q.OrderBy(t => t.Code).ThenByDescending(t => t.TemplateVersion).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<TemplateDto> SaveTemplateAsync(Guid? id, SaveTemplateRequest r, CancellationToken ct)
    {
        var v = new Validator().Required("code", r.Code, 40).Required("name", r.Name, 200)
            .Must(r.Fields is { Count: > 0 }, "fields", "Add at least one field.");
        foreach (var f in r.Fields ?? Array.Empty<TemplateField>())
        {
            v.Required("fields.key", f.Key, 50).Required("fields.label", f.Label, 200).OneOf("fields.type", f.Type, FieldTypes)
             .Must(f.Type != "select" || f.Options is { Count: > 0 }, "fields.options", $"Field '{f.Label}' needs options.");
        }
        v.Must(r.Fields is null || r.Fields.Select(f => f.Key).Distinct().Count() == r.Fields.Count, "fields", "Field keys must be unique.").ThrowIfInvalid();

        MonitoringTemplate t;
        if (id is null)
        {
            var latest = await _db.MonitoringTemplates.Where(x => x.Code == r.Code).MaxAsync(x => (int?)x.TemplateVersion, ct);
            t = new MonitoringTemplate { Code = r.Code, TemplateVersion = (latest ?? 0) + 1 };
            _db.MonitoringTemplates.Add(t);
        }
        else
        {
            t = await _db.MonitoringTemplates.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Template", id);
            if (t.Status != TemplateStatus.Draft)
            {
                // Published versions are retained unchanged; editing creates the next version (FR-ME-002).
                var latest = await _db.MonitoringTemplates.Where(x => x.Code == t.Code).MaxAsync(x => x.TemplateVersion, ct);
                t = new MonitoringTemplate { Code = t.Code, TemplateVersion = latest + 1 };
                _db.MonitoringTemplates.Add(t);
            }
        }
        t.Name = r.Name;
        t.ProjectType = r.ProjectType;
        t.FieldsJson = JsonSerializer.Serialize(r.Fields, Json);
        await _db.SaveChangesAsync(ct);
        return ToDto(t);
    }

    public async Task<TemplateDto> PublishTemplateAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.MonitoringTemplates.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Template", id);
        if (t.Status != TemplateStatus.Draft) throw new DomainException("Only a draft template can be published.", "FR-ME-002");
        foreach (var older in await _db.MonitoringTemplates.Where(x => x.Code == t.Code && x.Status == TemplateStatus.Published).ToListAsync(ct))
            older.Status = TemplateStatus.Retired;
        t.Status = TemplateStatus.Published;
        t.PublishedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(t);
    }

    // ----- Visits (FR-ME-003) -----
    public async Task<IReadOnlyList<VisitDto>> ListVisitsAsync(Guid? projectId, string? status, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.MonitoringVisits.AsNoTracking().InScope(scope, v => v.ProjectId);
        if (projectId is { } pid) q = q.Where(v => v.ProjectId == pid);
        if (Enum.TryParse<VisitStatus>(status, true, out var st)) q = q.Where(v => v.Status == st);
        return await ToDtosAsync(await q.OrderByDescending(v => v.ScheduledDate).ToListAsync(ct), ct);
    }

    public async Task<VisitDto> GetVisitAsync(Guid id, CancellationToken ct)
    {
        var v = await _db.MonitoringVisits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Visit", id);
        await _scope.EnsureProjectAsync(v.ProjectId, ct);
        return (await ToDtosAsync(new[] { v }, ct))[0];
    }

    public async Task<VisitDto> ScheduleVisitAsync(ScheduleVisitRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("projectId", r.ProjectId).ThrowIfInvalid();
        await _scope.EnsureProjectAsync(r.ProjectId, ct);
        MonitoringTemplate? template = null;
        if (r.TemplateId is { } tid)
        {
            template = await _db.MonitoringTemplates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tid, ct)
                       ?? throw new ValidationException("templateId", "Template not found.");
            if (template.Status != TemplateStatus.Published) throw new DomainException("Only published templates can be used for visits.", "FR-ME-002");
        }
        var visit = new MonitoringVisit
        {
            Number = await _numbers.NextAsync(NumberPrefixes.Visit, ct), ProjectId = r.ProjectId, TemplateId = template?.Id, TemplateVersion = template?.TemplateVersion,
            TemplateFieldsSnapshotJson = template?.FieldsJson, Type = r.Type, ScheduledDate = r.ScheduledDate, Officials = r.Officials, Location = r.Location,
            ProviderSupplierId = r.ProviderSupplierId
        };
        _db.MonitoringVisits.Add(visit);
        await _db.SaveChangesAsync(ct);
        return await GetVisitAsync(visit.Id, ct);
    }

    public async Task<VisitDto> CaptureVisitAsync(Guid id, CaptureVisitRequest r, CancellationToken ct)
    {
        new Validator().Required("officials", r.Officials, 1000).Required("summary", r.Summary, 8000).Required("outcome", r.Outcome, 50)
            .Range("latitude", r.Latitude, -90, 90).Range("longitude", r.Longitude, -180, 180).ThrowIfInvalid();
        var visit = await _db.MonitoringVisits.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Visit", id);
        await _scope.EnsureProjectAsync(visit.ProjectId, ct);
        if (visit.Status != VisitStatus.Scheduled) throw new DomainException("This visit has already been completed or cancelled.", "FR-ME-003");

        var fields = Fields(visit.TemplateFieldsSnapshotJson);
        if (r.Complete)
        {
            var v = new Validator();
            foreach (var f in fields.Where(f => f.Required))
                v.Must(r.Responses.TryGetValue(f.Key, out var value) && !string.IsNullOrWhiteSpace(value), $"responses.{f.Key}", $"'{f.Label}' is required.");
            v.ThrowIfInvalid();
        }
        visit.VisitDate = r.VisitDate;
        visit.Officials = r.Officials;
        visit.Location = r.Location;
        visit.Latitude = r.Latitude;
        visit.Longitude = r.Longitude;
        visit.ResponsesJson = JsonSerializer.Serialize(r.Responses, Json);
        visit.Summary = r.Summary;
        visit.Outcome = r.Outcome;
        if (r.Complete) visit.Status = VisitStatus.Completed;

        if (r.Complete && await _db.MePlans.SingleOrDefaultAsync(p => p.ProjectId == visit.ProjectId, ct) is { } plan)
        {
            plan.NextVisitDue = plan.Frequency switch
            {
                "Monthly" => r.VisitDate.AddMonths(1),
                "Quarterly" => r.VisitDate.AddMonths(3),
                "Bi-annually" => r.VisitDate.AddMonths(6),
                "Annually" => r.VisitDate.AddYears(1),
                _ => plan.NextVisitDue
            };
        }
        await _db.SaveChangesAsync(ct);
        return await GetVisitAsync(id, ct);
    }

    // ----- Findings & corrective actions (FR-ME-005/006) -----
    public async Task<IReadOnlyList<FindingDto>> ListFindingsAsync(Guid? projectId, bool openOnly, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.Findings.AsNoTracking().InScope(scope, f => f.ProjectId);
        if (projectId is { } pid) q = q.Where(f => f.ProjectId == pid);
        if (openOnly) q = q.Where(f => f.Status != FindingStatus.Closed);
        return await ToDtosAsync(await q.OrderByDescending(f => f.Severity).ThenByDescending(f => f.CreatedAtUtc).ToListAsync(ct), ct);
    }

    public async Task<FindingDto> SaveFindingAsync(Guid? id, SaveFindingRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("projectId", r.ProjectId).Required("description", r.Description, 4000).Optional("rootCause", r.RootCause, 4000).ThrowIfInvalid();
        await _scope.EnsureProjectAsync(r.ProjectId, ct);
        if (r.VisitId is { } vid && !await _db.MonitoringVisits.AnyAsync(v => v.Id == vid && v.ProjectId == r.ProjectId, ct))
            throw new ValidationException("visitId", "The visit does not belong to this project.");
        Finding f;
        if (id is null)
        {
            f = new Finding { Number = await _numbers.NextAsync(NumberPrefixes.Finding, ct), ProjectId = r.ProjectId, Source = r.VisitId is null ? "Desktop" : "Monitoring" };
            _db.Findings.Add(f);
        }
        else
        {
            f = await _db.Findings.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Finding", id);
            if (f.Status == FindingStatus.Closed) throw new DomainException("Closed findings are read-only.", "FR-ME-005");
        }
        f.VisitId = r.VisitId;
        f.Description = r.Description;
        f.Severity = r.Severity;
        f.RootCause = r.RootCause;
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { f }, ct))[0];
    }

    public async Task<IReadOnlyList<ActionDto>> ListActionsAsync(string? parentType, Guid? parentId, bool openOnly, bool mineOnly, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.CorrectiveActions.AsNoTracking().InScopeNullable(scope, a => a.ProjectId);
        if (!string.IsNullOrEmpty(parentType)) q = q.Where(a => a.ParentType == parentType);
        if (parentId is { } pid) q = q.Where(a => a.ParentId == pid);
        if (openOnly) q = q.Where(a => a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress);
        if (mineOnly && _user.UserId is { } me) q = q.Where(a => a.OwnerUserId == me);
        return await ToDtosAsync(await q.OrderBy(a => a.DueDate).ToListAsync(ct), ct);
    }

    public async Task<ActionDto> SaveActionAsync(Guid? id, SaveActionRequest r, CancellationToken ct)
    {
        new Validator().OneOf("parentType", r.ParentType, new[] { ParentTypes.Finding, ParentTypes.AuditFinding, ParentTypes.ContractBreach })
            .Required("description", r.Description, 4000).Required("ownerName", r.OwnerName, 200).ThrowIfInvalid();
        var projectId = await ParentProjectAsync(r.ParentType, r.ParentId, ct);
        if (projectId is { } pid) await _scope.EnsureProjectAsync(pid, ct);

        CorrectiveAction a;
        if (id is null)
        {
            a = new CorrectiveAction { Number = await _numbers.NextAsync(NumberPrefixes.CorrectiveAction, ct), ParentType = r.ParentType, ParentId = r.ParentId, ProjectId = projectId };
            _db.CorrectiveActions.Add(a);
            await MarkParentInProgressAsync(r.ParentType, r.ParentId, ct);
        }
        else
        {
            a = await _db.CorrectiveActions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Corrective action", id);
            if (a.Status is ActionStatus.Completed or ActionStatus.Cancelled) throw new DomainException("Completed actions are read-only.", "FR-ME-006");
        }
        a.Description = r.Description;
        a.OwnerUserId = r.OwnerUserId;
        a.OwnerName = r.OwnerName;
        a.DueDate = r.DueDate;
        if (r.OwnerUserId is { } owner)
        {
            await _notifier.NotifyUsersAsync(new[] { owner }, NotificationTemplates.ActionAssigned, new Dictionary<string, string?>
            {
                ["ItemType"] = "corrective action", ["Reference"] = a.Number, ["Description"] = a.Description, ["DueDate"] = a.DueDate.ToString("yyyy-MM-dd")
            }, "monitoring/actions", nameof(CorrectiveAction), a.Id, ct);
        }
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { a }, ct))[0];
    }

    public async Task<ActionDto> CompleteActionAsync(Guid id, CompleteActionRequest r, CancellationToken ct)
    {
        new Validator().Required("closureNotes", r.ClosureNotes, 4000).ThrowIfInvalid();
        var a = await _db.CorrectiveActions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Corrective action", id);
        if (a.ProjectId is { } pid) await _scope.EnsureProjectAsync(pid, ct);
        if (a.Status is ActionStatus.Completed or ActionStatus.Cancelled) throw new DomainException("The action is already closed.", "FR-ME-006");
        var hasEvidence = await _db.Evidence.AnyAsync(e => e.ParentType == ParentTypes.CorrectiveAction && e.ParentId == id
            && e.VerificationStatus != EvidenceStatus.Rejected, ct);
        if (!hasEvidence) throw new DomainException("Attach closure evidence before completing the corrective action.", "FR-ME-006");

        a.Status = ActionStatus.Completed;
        a.ClosureNotes = r.ClosureNotes;
        a.CompletedAtUtc = _clock.UtcNow;

        // Close the parent finding once all its actions are complete (tracked to closure).
        var siblingsOpen = await _db.CorrectiveActions.AnyAsync(x => x.ParentType == a.ParentType && x.ParentId == a.ParentId && x.Id != a.Id
            && (x.Status == ActionStatus.Open || x.Status == ActionStatus.InProgress), ct);
        if (!siblingsOpen)
        {
            if (a.ParentType == ParentTypes.Finding && await _db.Findings.SingleOrDefaultAsync(f => f.Id == a.ParentId, ct) is { } finding)
            {
                finding.Status = FindingStatus.Closed;
                finding.ClosedAtUtc = _clock.UtcNow;
            }
            else if (a.ParentType == ParentTypes.AuditFinding && await _db.AuditFindings.SingleOrDefaultAsync(f => f.Id == a.ParentId, ct) is { } audit)
            {
                audit.Status = FindingStatus.Closed;
                audit.ClosedAtUtc = _clock.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { a }, ct))[0];
    }

    // ----- Beneficiaries (FR-ME-007/008/009, SEC-011) -----
    public async Task<PagedResult<BeneficiaryDto>> ListBeneficiariesAsync(Guid? projectId, string? status, string? search, int page, int pageSize, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.Beneficiaries.AsNoTracking().InScope(scope, b => b.ProjectId);
        if (projectId is { } pid) q = q.Where(b => b.ProjectId == pid);
        if (Enum.TryParse<BeneficiaryStatus>(status, true, out var st)) q = q.Where(b => b.Status == st);
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Search by number or by exact identifier (matched through the keyed hash, never plaintext).
            var hash = _protector.KeyedHash(search);
            q = q.Where(b => b.Number.Contains(search) || b.IdentifierHash == hash);
        }
        var total = await q.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var rows = await q.OrderByDescending(b => b.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<BeneficiaryDto>(await ToDtosAsync(rows, ct), total, page, pageSize);
    }

    public async Task<BeneficiaryDto> RegisterBeneficiaryAsync(SaveBeneficiaryRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("projectId", r.ProjectId).Required("identifier", r.Identifier, 30).OneOf("identifierType", r.IdentifierType, new[] { "SAID", "Passport", "Other" })
            .Required("firstName", r.FirstName, 100).Required("lastName", r.LastName, 100).Required("intervention", r.Intervention, 200)
            .Must(r.ConsentObtained, "consentObtained", "Record POPIA consent before capturing personal information.")
            .Must(r.IdentifierType != "SAID" || IsValidSaId(r.Identifier), "identifier", "Enter a valid 13-digit South African ID number.").ThrowIfInvalid();
        await _scope.EnsureProjectAsync(r.ProjectId, ct);

        var hash = _protector.KeyedHash(r.Identifier);
        var matches = await (from b in _db.Beneficiaries.AsNoTracking()
                             join p in _db.Projects.AsNoTracking() on b.ProjectId equals p.Id
                             where b.IdentifierHash == hash
                             select new { b.Number, b.ProjectId, b.Intervention, b.FundingSource, b.Status, Ref = p.ProjectNumber ?? p.DraftReference }).ToListAsync(ct);
        var sameProject = matches.Where(m => m.ProjectId == r.ProjectId).ToList();
        if (sameProject.Any(m => m.Intervention == r.Intervention && m.Status is not (BeneficiaryStatus.Completed or BeneficiaryStatus.DroppedOut)))
            throw new ConflictException($"This person is already registered on this project for {r.Intervention} ({sameProject[0].Number}).", "FR-ME-008");

        var beneficiary = new Beneficiary
        {
            Number = await _numbers.NextAsync(NumberPrefixes.Beneficiary, ct), ProjectId = r.ProjectId, IdentifierEncrypted = _protector.Protect(r.Identifier.Trim()),
            IdentifierHash = hash, IdentifierMasked = Masking.Mask(r.Identifier.Trim(), 3), IdentifierType = r.IdentifierType, FirstName = r.FirstName.Trim(),
            LastName = r.LastName.Trim(), Gender = r.Gender, BirthYear = r.BirthYear, Province = r.Province, District = r.District, Intervention = r.Intervention,
            ProviderSupplierId = r.ProviderSupplierId, FundingSource = r.FundingSource, ConsentObtained = r.ConsentObtained
        };
        // Configurable duplicate condition: same person funded under another project/intervention is flagged for review, not blocked.
        if (matches.Count > 0)
        {
            beneficiary.PotentialDuplicate = true;
            beneficiary.DuplicateNote = "Also registered as " + string.Join(", ", matches.Select(m => $"{m.Number} on {m.Ref} ({m.Intervention}, {m.FundingSource ?? "n/a"})"));
        }
        _db.Beneficiaries.Add(beneficiary);
        _db.BeneficiaryStatusHistory.Add(new BeneficiaryStatusHistory
        {
            BeneficiaryId = beneficiary.Id, FromStatus = BeneficiaryStatus.Registered, ToStatus = BeneficiaryStatus.Registered, ChangedAtUtc = _clock.UtcNow,
            ChangedBy = _user.DisplayName ?? _user.Username, Note = "Registered"
        });
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { beneficiary }, ct))[0];
    }

    public async Task<BeneficiaryDto> ChangeBeneficiaryStatusAsync(Guid id, BeneficiaryStatusRequest r, CancellationToken ct)
    {
        var b = await _db.Beneficiaries.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Beneficiary", id);
        await _scope.EnsureProjectAsync(b.ProjectId, ct);
        var allowed = (b.Status, r.Status) switch
        {
            (BeneficiaryStatus.Registered, BeneficiaryStatus.Enrolled) => true,
            (BeneficiaryStatus.Enrolled, BeneficiaryStatus.Participating) => true,
            (BeneficiaryStatus.Participating, BeneficiaryStatus.Completed) => true,
            (BeneficiaryStatus.Completed, BeneficiaryStatus.Placed) => true,
            (BeneficiaryStatus.Registered or BeneficiaryStatus.Enrolled or BeneficiaryStatus.Participating, BeneficiaryStatus.DroppedOut) => true,
            _ => false
        };
        if (!allowed) throw new DomainException($"A beneficiary cannot move from {b.Status} to {r.Status}.", "FR-ME-009");
        _db.BeneficiaryStatusHistory.Add(new BeneficiaryStatusHistory
        {
            BeneficiaryId = id, FromStatus = b.Status, ToStatus = r.Status, ChangedAtUtc = _clock.UtcNow, ChangedBy = _user.DisplayName ?? _user.Username, Note = r.Note
        });
        b.Status = r.Status;
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { b }, ct))[0];
    }

    public async Task<IReadOnlyList<BeneficiaryHistoryDto>> BeneficiaryHistoryAsync(Guid id, CancellationToken ct)
    {
        var b = await _db.Beneficiaries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Beneficiary", id);
        await _scope.EnsureProjectAsync(b.ProjectId, ct);
        return await _db.BeneficiaryStatusHistory.AsNoTracking().Where(h => h.BeneficiaryId == id).OrderBy(h => h.ChangedAtUtc)
            .Select(h => new BeneficiaryHistoryDto(h.FromStatus.ToString(), h.ToStatus.ToString(), h.ChangedAtUtc, h.ChangedBy, h.Note)).ToListAsync(ct);
    }

    /// <summary>Full identifier only for PII-authorised roles, with a stated purpose that is audited (POPIA purpose limitation).</summary>
    public async Task<string> RevealIdentifierAsync(Guid id, string reason, CancellationToken ct)
    {
        new Validator().Required("reason", reason, 500).ThrowIfInvalid();
        if (!_user.HasPermission(Permissions.BeneficiaryPii)) throw new ForbiddenException("You are not authorised to view personal information.", "SEC-011");
        var b = await _db.Beneficiaries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Beneficiary", id);
        await _scope.EnsureProjectAsync(b.ProjectId, ct);
        _audit.Write("Monitoring", nameof(Beneficiary), id.ToString(), "PiiRevealed", new { b.Number }, reason);
        await _db.SaveChangesAsync(ct);
        return _protector.Unprotect(b.IdentifierEncrypted);
    }

    public async Task<BeneficiaryDto> ResolveDuplicateAsync(Guid id, bool isDuplicate, string note, CancellationToken ct)
    {
        new Validator().Required("note", note, 1000).ThrowIfInvalid();
        var b = await _db.Beneficiaries.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Beneficiary", id);
        await _scope.EnsureProjectAsync(b.ProjectId, ct);
        b.PotentialDuplicate = false;
        b.DuplicateNote = (isDuplicate ? "Confirmed duplicate: " : "Reviewed, not a duplicate: ") + note;
        if (isDuplicate && b.Status is BeneficiaryStatus.Registered or BeneficiaryStatus.Enrolled)
        {
            _db.BeneficiaryStatusHistory.Add(new BeneficiaryStatusHistory
            {
                BeneficiaryId = id, FromStatus = b.Status, ToStatus = BeneficiaryStatus.DroppedOut, ChangedAtUtc = _clock.UtcNow,
                ChangedBy = _user.DisplayName ?? _user.Username, Note = "Removed as duplicate"
            });
            b.Status = BeneficiaryStatus.DroppedOut;
        }
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { b }, ct))[0];
    }

    // ----- Dashboard (FR-ME-010) -----
    public async Task<MeDashboardDto> DashboardAsync(Guid? programmeId, Guid? projectId, Guid? providerId, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var projectsQ = _db.Projects.AsNoTracking().InScope(scope, p => p.Id);
        if (programmeId is { } prog) projectsQ = projectsQ.Where(p => p.ProgrammeId == prog);
        if (projectId is { } pid) projectsQ = projectsQ.Where(p => p.Id == pid);
        var ids = await projectsQ.Select(p => p.Id).ToListAsync(ct);
        var today = _clock.Today;

        var visitsQ = _db.MonitoringVisits.AsNoTracking().Where(v => ids.Contains(v.ProjectId));
        if (providerId is { } prov) visitsQ = visitsQ.Where(v => v.ProviderSupplierId == prov);
        var visits = await visitsQ.Select(v => new { v.Status, v.ScheduledDate, v.VisitDate, v.Outcome }).ToListAsync(ct);
        var findings = await _db.Findings.AsNoTracking().Where(f => ids.Contains(f.ProjectId)).Select(f => new { f.Status, f.Severity }).ToListAsync(ct);
        var actions = await _db.CorrectiveActions.AsNoTracking().Where(a => a.ProjectId != null && ids.Contains(a.ProjectId.Value)
            && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress)).ToListAsync(ct);
        var benQ = _db.Beneficiaries.AsNoTracking().Where(b => ids.Contains(b.ProjectId));
        if (providerId is { } prov2) benQ = benQ.Where(b => b.ProviderSupplierId == prov2);
        var bens = await benQ.Select(b => new { b.Status, b.PotentialDuplicate }).ToListAsync(ct);
        var overdueActions = actions.Where(a => a.DueDate < today).ToList();

        var months = Enumerable.Range(0, 12).Select(i => new DateOnly(today.Year, today.Month, 1).AddMonths(i - 11)).ToList();
        return new MeDashboardDto(
            visits.Count(v => v.Status == VisitStatus.Scheduled), visits.Count(v => v.Status == VisitStatus.Completed),
            visits.Count(v => v.Status == VisitStatus.Scheduled && v.ScheduledDate < today),
            findings.Count(f => f.Status != FindingStatus.Closed), findings.Count(f => f.Status != FindingStatus.Closed && f.Severity == Severity.Critical),
            overdueActions.Count, bens.Count, bens.Count(b => b.Status is BeneficiaryStatus.Completed or BeneficiaryStatus.Placed), bens.Count(b => b.PotentialDuplicate),
            Enum.GetValues<Severity>().Select(s => new NamedCount(s.ToString(), findings.Count(f => f.Severity == s && f.Status != FindingStatus.Closed))).ToList(),
            Enum.GetValues<BeneficiaryStatus>().Select(s => new NamedCount(s.ToString(), bens.Count(b => b.Status == s))).ToList(),
            months.Select(m => new TrendPoint($"{m.Year}-{m.Month:D2}", visits.Count(v => v.VisitDate is { } d && d.Year == m.Year && d.Month == m.Month))).ToList(),
            months.Select(m => new TrendPoint($"{m.Year}-{m.Month:D2}", visits.Count(v => v.VisitDate is { } d && d.Year == m.Year && d.Month == m.Month
                && v.Outcome == "Satisfactory"))).ToList(),
            (await ToDtosAsync(overdueActions.OrderBy(a => a.DueDate).Take(20).ToList(), ct)));
    }

    // ----- helpers -----
    internal static bool IsValidSaId(string id)
    {
        id = id.Trim();
        if (id.Length != 13 || !id.All(char.IsDigit)) return false;
        if (!DateOnly.TryParseExact(id[..6], "yyMMdd", out _)) return false;
        // Luhn checksum
        var sum = 0;
        for (var i = 0; i < 13; i++)
        {
            var digit = id[12 - i] - '0';
            if (i % 2 == 1)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
        }
        return sum % 10 == 0;
    }

    private async Task<Guid?> ParentProjectAsync(string parentType, Guid parentId, CancellationToken ct) => parentType switch
    {
        ParentTypes.Finding => await _db.Findings.Where(f => f.Id == parentId).Select(f => (Guid?)f.ProjectId).SingleOrDefaultAsync(ct)
                               ?? throw new ValidationException("parentId", "Finding not found."),
        ParentTypes.AuditFinding => (await _db.AuditFindings.Where(f => f.Id == parentId).Select(f => new { f.ProjectId }).SingleOrDefaultAsync(ct)
                                     ?? throw new ValidationException("parentId", "Audit finding not found.")).ProjectId,
        ParentTypes.ContractBreach => await (from b in _db.ContractBreaches
                                             join c in _db.Contracts on b.ContractId equals c.Id
                                             where b.Id == parentId
                                             select (Guid?)c.ProjectId).SingleOrDefaultAsync(ct)
                                      ?? throw new ValidationException("parentId", "Breach not found."),
        _ => null
    };

    private async Task MarkParentInProgressAsync(string parentType, Guid parentId, CancellationToken ct)
    {
        if (parentType == ParentTypes.Finding && await _db.Findings.SingleOrDefaultAsync(f => f.Id == parentId, ct) is { Status: FindingStatus.Open } f)
            f.Status = FindingStatus.ActionInProgress;
        else if (parentType == ParentTypes.AuditFinding && await _db.AuditFindings.SingleOrDefaultAsync(x => x.Id == parentId, ct) is { Status: FindingStatus.Open } af)
            af.Status = FindingStatus.ActionInProgress;
    }

    private static IReadOnlyList<TemplateField> Fields(string? json) =>
        string.IsNullOrWhiteSpace(json) ? Array.Empty<TemplateField>() : JsonSerializer.Deserialize<List<TemplateField>>(json, Json) ?? new List<TemplateField>();

    private static MePlanDto ToDto(MePlan p) =>
        new(p.Id, p.ProjectId, p.Description, p.Frequency, p.Methods, p.Indicators, p.ResponsibleOfficerUserId, p.ResponsibleOfficerName, p.NextVisitDue, p.Version);

    private static TemplateDto ToDto(MonitoringTemplate t) =>
        new(t.Id, t.Code, t.Name, t.ProjectType, t.TemplateVersion, t.Status.ToString(), Fields(t.FieldsJson), t.PublishedAtUtc, t.Version);

    private async Task<List<VisitDto>> ToDtosAsync(IReadOnlyCollection<MonitoringVisit> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = p.ProjectNumber ?? p.DraftReference }).ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        var findings = await _db.Findings.AsNoTracking().Where(f => f.VisitId != null && ids.Contains(f.VisitId.Value)).Select(f => f.VisitId!.Value).ToListAsync(ct);
        var evidence = await _db.Evidence.AsNoTracking().Where(e => e.ParentType == ParentTypes.MonitoringVisit && ids.Contains(e.ParentId)).Select(e => e.ParentId).ToListAsync(ct);
        return rows.Select(v => new VisitDto(v.Id, v.Number, v.ProjectId, projects.GetValueOrDefault(v.ProjectId, "?"), v.TemplateId, v.TemplateVersion,
            Fields(v.TemplateFieldsSnapshotJson), v.Type.ToString(), v.ScheduledDate, v.VisitDate, v.Officials, v.Location, v.Latitude, v.Longitude,
            string.IsNullOrWhiteSpace(v.ResponsesJson) ? new Dictionary<string, string?>() : JsonSerializer.Deserialize<Dictionary<string, string?>>(v.ResponsesJson, Json)!,
            v.Summary, v.Outcome, v.Status.ToString(), v.ProviderSupplierId, findings.Count(f => f == v.Id), evidence.Count(e => e == v.Id), v.Version)).ToList();
    }

    private async Task<List<FindingDto>> ToDtosAsync(IReadOnlyCollection<Finding> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = p.ProjectNumber ?? p.DraftReference }).ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        var open = await _db.CorrectiveActions.AsNoTracking().Where(a => a.ParentType == ParentTypes.Finding && ids.Contains(a.ParentId)
            && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress)).Select(a => a.ParentId).ToListAsync(ct);
        return rows.Select(f => new FindingDto(f.Id, f.Number, f.ProjectId, projects.GetValueOrDefault(f.ProjectId, "?"), f.VisitId, f.Source, f.Description,
            f.Severity.ToString(), f.RootCause, f.Status.ToString(), open.Count(o => o == f.Id), f.CreatedAtUtc, f.Version)).ToList();
    }

    private async Task<List<ActionDto>> ToDtosAsync(IReadOnlyCollection<CorrectiveAction> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var evidence = await _db.Evidence.AsNoTracking().Where(e => e.ParentType == ParentTypes.CorrectiveAction && ids.Contains(e.ParentId))
            .Select(e => e.ParentId).ToListAsync(ct);
        var today = _clock.Today;
        return rows.Select(a => new ActionDto(a.Id, a.Number, a.ParentType, a.ParentId, a.ProjectId, a.Description, a.OwnerUserId, a.OwnerName, a.DueDate,
            a.Status.ToString(), a.ClosureNotes, a.CompletedAtUtc, a.EscalationLevel, a.IsOverdue(today), evidence.Count(e => e == a.Id), a.Version)).ToList();
    }

    private async Task<List<BeneficiaryDto>> ToDtosAsync(IReadOnlyCollection<Beneficiary> rows, CancellationToken ct)
    {
        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = p.ProjectNumber ?? p.DraftReference }).ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        // Names are masked for roles without PII permission (SRS §7.1 minimisation).
        var pii = _user.HasPermission(Permissions.BeneficiaryPii);
        return rows.Select(b => new BeneficiaryDto(b.Id, b.Number, b.ProjectId, projects.GetValueOrDefault(b.ProjectId, "?"), b.IdentifierMasked, b.IdentifierType,
            pii ? b.FirstName : Masking.Mask(b.FirstName, 1), pii ? b.LastName : Masking.Mask(b.LastName, 1), b.Gender, b.BirthYear, b.Province, b.District,
            b.Intervention, b.ProviderSupplierId, b.FundingSource, b.Status.ToString(), b.PotentialDuplicate, b.DuplicateNote, b.ConsentObtained, pii,
            b.Version)).ToList();
    }
}
