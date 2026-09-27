using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Engines;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Monitoring;

namespace Teta.Ippcms.Application.Assurance;

// ---------- DTOs ----------
public sealed record BandDto(Guid Id, string Name, int MinScore, int MaxScore, string Colour, int SortOrder);
public sealed record SaveBandsRequest(IReadOnlyList<BandInput> Bands);
public sealed record BandInput(string Name, int MinScore, int MaxScore, string Colour);

public sealed record RiskDto(Guid Id, string Number, string ParentType, Guid ParentId, Guid? ProjectId, string? ProjectReference, string Title, string Cause,
    string Event, string Consequence, string? Category, int InherentLikelihood, int InherentImpact, int InherentScore, string InherentRating,
    int ResidualLikelihood, int ResidualImpact, int ResidualScore, string ResidualRating, Guid? OwnerUserId, string OwnerName, DateOnly ReviewDate,
    string Status, bool ReviewOverdue, int OpenTreatments, int OverdueTreatments, long Version);
public sealed record SaveRiskRequest(string ParentType, Guid ParentId, string Title, string Cause, string Event, string Consequence, string? Category,
    int InherentLikelihood, int InherentImpact, int ResidualLikelihood, int ResidualImpact, Guid? OwnerUserId, string OwnerName, DateOnly ReviewDate,
    RiskStatus? Status);

public sealed record ControlDto(Guid Id, Guid RiskId, string Name, string? Description, Guid? OwnerUserId, string OwnerName, string Effectiveness,
    DateOnly? LastAssessedOn, IReadOnlyList<AssessmentDto> History);
public sealed record AssessmentDto(DateOnly AssessedOn, string Effectiveness, string? Comment, string? AssessedBy);
public sealed record SaveControlRequest(string Name, string? Description, Guid? OwnerUserId, string OwnerName);
public sealed record AssessControlRequest(string Effectiveness, string? Comment);

public sealed record TreatmentDto(Guid Id, Guid RiskId, string Description, Guid? OwnerUserId, string OwnerName, DateOnly DueDate, string Status,
    DateTime? CompletedAtUtc, int EscalationLevel, bool IsOverdue);
public sealed record SaveTreatmentRequest(string Description, Guid? OwnerUserId, string OwnerName, DateOnly DueDate, ActionStatus? Status);

public sealed record HeatCell(int Likelihood, int Impact, int Score, string Rating, string Colour, int Count);
public sealed record HeatMapDto(string Basis, IReadOnlyList<HeatCell> Cells, IReadOnlyList<BandDto> Bands);

public sealed record ObligationDto(Guid Id, string Code, string Title, string Source, string? Description, string Frequency, Guid? OwnerUserId,
    string OwnerName, bool IsActive, string? LastPeriod, string? LastResult, DateTime? LastAttestedAtUtc);
public sealed record SaveObligationRequest(string Code, string Title, string Source, string? Description, string Frequency, Guid? OwnerUserId,
    string OwnerName, bool IsActive);
public sealed record AttestationDto(Guid Id, Guid ObligationId, string ObligationCode, string Period, string Result, string? Comment,
    DateTime AttestedAtUtc, string AttestedBy);
public sealed record AttestRequest(string Period, AttestationResult Result, string? Comment);

public sealed record AuditFindingDto(Guid Id, string Number, Guid? ProjectId, string? ProjectReference, string Process, string Source, string? AuditReference,
    string Title, string Description, string? Recommendation, string Rating, string? ManagementResponse, Guid? ActionOwnerUserId, string ActionOwnerName,
    DateOnly DueDate, string Status, bool IsOverdue, int OpenActions, DateTime? ClosedAtUtc, long Version);
public sealed record SaveAuditFindingRequest(Guid? ProjectId, string Process, AuditSource Source, string? AuditReference, string Title, string Description,
    string? Recommendation, Severity Rating, string? ManagementResponse, Guid? ActionOwnerUserId, string ActionOwnerName, DateOnly DueDate);

public sealed record CoverageDto(Guid Id, string Area, Guid? RiskId, string Line, string Period, bool Covered, string? Provider, string? Rating, string? Comments);
public sealed record SaveCoverageRequest(string Area, Guid? RiskId, AssuranceLine Line, string Period, bool Covered, string? Provider, string? Rating, string? Comments);
public sealed record CoverageMapDto(string Period, IReadOnlyList<string> Lines, IReadOnlyList<CoverageRow> Rows, int Gaps);
public sealed record CoverageRow(string Area, IReadOnlyDictionary<string, bool> CoveredByLine, bool HasGap);

public sealed record AssuranceDashboardDto(int OpenRisks, int CriticalRisks, int HighRisks, int OverdueTreatments, int OverdueReviews, int OpenAuditFindings,
    int OverdueAuditActions, int NonCompliantAttestations, int CoverageGaps, HeatMapDto HeatMap, IReadOnlyList<RiskDto> TopRisks,
    IReadOnlyList<AuditFindingDto> OverdueFindings);

public interface IRiskService
{
    Task<IReadOnlyList<BandDto>> GetBandsAsync(CancellationToken ct);
    Task<IReadOnlyList<BandDto>> SaveBandsAsync(SaveBandsRequest request, CancellationToken ct);

    Task<IReadOnlyList<RiskDto>> ListRisksAsync(Guid? projectId, string? parentType, Guid? parentId, string? rating, bool openOnly, CancellationToken ct);
    Task<RiskDto> GetRiskAsync(Guid id, CancellationToken ct);
    Task<RiskDto> SaveRiskAsync(Guid? id, SaveRiskRequest request, CancellationToken ct);
    Task<IReadOnlyList<ControlDto>> ListControlsAsync(Guid riskId, CancellationToken ct);
    Task<ControlDto> SaveControlAsync(Guid riskId, Guid? id, SaveControlRequest request, CancellationToken ct);
    Task<ControlDto> AssessControlAsync(Guid riskId, Guid controlId, AssessControlRequest request, CancellationToken ct);
    Task<IReadOnlyList<TreatmentDto>> ListTreatmentsAsync(Guid riskId, CancellationToken ct);
    Task<TreatmentDto> SaveTreatmentAsync(Guid riskId, Guid? id, SaveTreatmentRequest request, CancellationToken ct);
    Task<HeatMapDto> HeatMapAsync(Guid? projectId, bool residual, CancellationToken ct);

    Task<IReadOnlyList<ObligationDto>> ListObligationsAsync(CancellationToken ct);
    Task<ObligationDto> SaveObligationAsync(Guid? id, SaveObligationRequest request, CancellationToken ct);
    Task<IReadOnlyList<AttestationDto>> ListAttestationsAsync(Guid? obligationId, string? period, CancellationToken ct);
    Task<AttestationDto> AttestAsync(Guid obligationId, AttestRequest request, CancellationToken ct);

    Task<IReadOnlyList<AuditFindingDto>> ListAuditFindingsAsync(Guid? projectId, bool openOnly, CancellationToken ct);
    Task<AuditFindingDto> SaveAuditFindingAsync(Guid? id, SaveAuditFindingRequest request, CancellationToken ct);

    Task<CoverageMapDto> CoverageMapAsync(string period, CancellationToken ct);
    Task<CoverageDto> SaveCoverageAsync(Guid? id, SaveCoverageRequest request, CancellationToken ct);

    Task<AssuranceDashboardDto> DashboardAsync(Guid? projectId, CancellationToken ct);
}

/// <summary>Risk, compliance and assurance (SRS §5.9).</summary>
public sealed class RiskService : IRiskService
{
    private static readonly string[] Effectiveness = { "Effective", "PartiallyEffective", "NotEffective", "NotAssessed" };

    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAccessScope _scope;
    private readonly INumberGenerator _numbers;
    private readonly INotifier _notifier;

    public RiskService(ITetaDbContext db, ICurrentUser user, IClock clock, IAccessScope scope, INumberGenerator numbers, INotifier notifier)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _scope = scope;
        _numbers = numbers;
        _notifier = notifier;
    }

    // ----- Matrix (FR-RSK-002) -----
    public async Task<IReadOnlyList<BandDto>> GetBandsAsync(CancellationToken ct)
    {
        var bands = await _db.RiskRatingBands.AsNoTracking().OrderBy(b => b.MinScore).ToListAsync(ct);
        return bands.Count > 0
            ? bands.Select(b => new BandDto(b.Id, b.Name, b.MinScore, b.MaxScore, b.Colour, b.SortOrder)).ToList()
            : RiskRating.DefaultBands.Select((b, i) => new BandDto(Guid.Empty, b.Name, b.MinScore, b.MaxScore, b.Colour, i)).ToList();
    }

    public async Task<IReadOnlyList<BandDto>> SaveBandsAsync(SaveBandsRequest r, CancellationToken ct)
    {
        var bands = r.Bands.Select(b => new RatingBand(b.Name, b.MinScore, b.MaxScore, b.Colour)).ToList();
        RiskRating.ValidateBands(bands);
        foreach (var existing in await _db.RiskRatingBands.ToListAsync(ct)) _db.RiskRatingBands.Remove(existing);
        var order = 0;
        foreach (var b in bands.OrderBy(b => b.MinScore))
            _db.RiskRatingBands.Add(new RiskRatingBand { Name = b.Name, MinScore = b.MinScore, MaxScore = b.MaxScore, Colour = b.Colour, SortOrder = order++ });

        // Re-rate open risks so ratings stay consistent with the configured matrix.
        foreach (var risk in await _db.Risks.Where(x => x.Status == RiskStatus.Open || x.Status == RiskStatus.Treating).ToListAsync(ct))
        {
            (risk.InherentScore, risk.InherentRating) = RiskRating.Rate(risk.InherentLikelihood, risk.InherentImpact, bands);
            (risk.ResidualScore, risk.ResidualRating) = RiskRating.Rate(risk.ResidualLikelihood, risk.ResidualImpact, bands);
        }
        await _db.SaveChangesAsync(ct);
        return await GetBandsAsync(ct);
    }

    // ----- Risk register (FR-RSK-001, FR-CON-012) -----
    public async Task<IReadOnlyList<RiskDto>> ListRisksAsync(Guid? projectId, string? parentType, Guid? parentId, string? rating, bool openOnly, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.Risks.AsNoTracking().InScopeNullable(scope, r => r.ProjectId);
        if (projectId is { } pid) q = q.Where(r => r.ProjectId == pid);
        if (!string.IsNullOrEmpty(parentType)) q = q.Where(r => r.ParentType == parentType);
        if (parentId is { } par) q = q.Where(r => r.ParentId == par);
        if (!string.IsNullOrEmpty(rating)) q = q.Where(r => r.ResidualRating == rating);
        if (openOnly) q = q.Where(r => r.Status == RiskStatus.Open || r.Status == RiskStatus.Treating);
        return await ToDtosAsync(await q.OrderByDescending(r => r.ResidualScore).ToListAsync(ct), ct);
    }

    public async Task<RiskDto> GetRiskAsync(Guid id, CancellationToken ct)
    {
        var r = await _db.Risks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Risk", id);
        if (r.ProjectId is { } pid) await _scope.EnsureProjectAsync(pid, ct);
        return (await ToDtosAsync(new[] { r }, ct))[0];
    }

    public async Task<RiskDto> SaveRiskAsync(Guid? id, SaveRiskRequest r, CancellationToken ct)
    {
        new Validator().OneOf("parentType", r.ParentType, new[] { ParentTypes.Project, ParentTypes.Contract, ParentTypes.Programme })
            .RequiredId("parentId", r.ParentId).Required("title", r.Title, 300).Required("cause", r.Cause, 4000).Required("event", r.Event, 4000)
            .Required("consequence", r.Consequence, 4000).Required("ownerName", r.OwnerName, 200)
            .Must(r.ReviewDate != default, "reviewDate", "A review date is required.").ThrowIfInvalid();
        var projectId = r.ParentType switch
        {
            ParentTypes.Project => await _db.Projects.Where(p => p.Id == r.ParentId).Select(p => (Guid?)p.Id).SingleOrDefaultAsync(ct)
                                   ?? throw new ValidationException("parentId", "Project not found."),
            ParentTypes.Contract => await _db.Contracts.Where(c => c.Id == r.ParentId).Select(c => (Guid?)c.ProjectId).SingleOrDefaultAsync(ct)
                                    ?? throw new ValidationException("parentId", "Contract not found."),
            _ => await _db.Programmes.AnyAsync(p => p.Id == r.ParentId, ct) ? (Guid?)null : throw new ValidationException("parentId", "Programme not found.")
        };
        if (projectId is { } pid) await _scope.EnsureProjectAsync(pid, ct);

        var bands = (await GetBandsAsync(ct)).Select(b => new RatingBand(b.Name, b.MinScore, b.MaxScore, b.Colour)).ToList();
        var (inherentScore, inherentRating) = RiskRating.Rate(r.InherentLikelihood, r.InherentImpact, bands);
        var (residualScore, residualRating) = RiskRating.Rate(r.ResidualLikelihood, r.ResidualImpact, bands);
        if (residualScore > inherentScore)
            throw new ValidationException("residualLikelihood", "Residual rating cannot exceed the inherent rating.");

        Risk risk;
        if (id is null)
        {
            risk = new Risk { Number = await _numbers.NextAsync(NumberPrefixes.Risk, ct) };
            _db.Risks.Add(risk);
        }
        else
        {
            risk = await _db.Risks.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Risk", id);
        }
        risk.ParentType = r.ParentType;
        risk.ParentId = r.ParentId;
        risk.ProjectId = projectId;
        risk.Title = r.Title;
        risk.Cause = r.Cause;
        risk.Event = r.Event;
        risk.Consequence = r.Consequence;
        risk.Category = r.Category;
        risk.InherentLikelihood = r.InherentLikelihood;
        risk.InherentImpact = r.InherentImpact;
        risk.InherentScore = inherentScore;
        risk.InherentRating = inherentRating;
        risk.ResidualLikelihood = r.ResidualLikelihood;
        risk.ResidualImpact = r.ResidualImpact;
        risk.ResidualScore = residualScore;
        risk.ResidualRating = residualRating;
        risk.OwnerUserId = r.OwnerUserId;
        risk.OwnerName = r.OwnerName;
        risk.ReviewDate = r.ReviewDate;
        if (r.Status is { } s) risk.Status = s;

        // Critical contract/project risks escalate immediately (FR-CON-012).
        if (residualRating == "Critical" && risk.EscalatedAtUtc is null)
        {
            risk.EscalatedAtUtc = _clock.UtcNow;
            _db.EscalationEvents.Add(new Domain.Workflow.EscalationEvent
            {
                EntityType = nameof(Risk), EntityId = risk.Id, EntityReference = risk.Number, Level = 1, EscalatedToRole = Domain.Security.Roles.HeadPmo,
                Reason = $"Critical residual risk: {risk.Title}", OccurredAtUtc = _clock.UtcNow
            });
            await _notifier.NotifyRoleAsync(Domain.Security.Roles.HeadPmo, NotificationTemplates.OverdueItemEscalated, new Dictionary<string, string?>
            {
                ["ItemType"] = "critical risk", ["Reference"] = risk.Number, ["Description"] = risk.Title, ["DueDate"] = risk.ReviewDate.ToString("yyyy-MM-dd"), ["Level"] = "1"
            }, "risks", nameof(Risk), risk.Id, projectId, ct);
        }
        await _db.SaveChangesAsync(ct);
        return await GetRiskAsync(risk.Id, ct);
    }

    public async Task<IReadOnlyList<ControlDto>> ListControlsAsync(Guid riskId, CancellationToken ct)
    {
        await GetRiskAsync(riskId, ct);
        var controls = await _db.RiskControls.AsNoTracking().Where(c => c.RiskId == riskId).OrderBy(c => c.Name).ToListAsync(ct);
        var ids = controls.Select(c => c.Id).ToList();
        var history = await _db.ControlAssessments.AsNoTracking().Where(a => ids.Contains(a.ControlId)).OrderByDescending(a => a.AssessedOn).ToListAsync(ct);
        return controls.Select(c => new ControlDto(c.Id, c.RiskId, c.Name, c.Description, c.OwnerUserId, c.OwnerName, c.Effectiveness, c.LastAssessedOn,
            history.Where(h => h.ControlId == c.Id).Select(h => new AssessmentDto(h.AssessedOn, h.Effectiveness, h.Comment, h.AssessedBy)).ToList())).ToList();
    }

    public async Task<ControlDto> SaveControlAsync(Guid riskId, Guid? id, SaveControlRequest r, CancellationToken ct)
    {
        new Validator().Required("name", r.Name, 300).Required("ownerName", r.OwnerName, 200).ThrowIfInvalid();
        await GetRiskAsync(riskId, ct);
        RiskControl c;
        if (id is null)
        {
            c = new RiskControl { RiskId = riskId };
            _db.RiskControls.Add(c);
        }
        else
        {
            c = await _db.RiskControls.SingleOrDefaultAsync(x => x.Id == id && x.RiskId == riskId, ct) ?? throw new NotFoundException("Control", id);
        }
        c.Name = r.Name;
        c.Description = r.Description;
        c.OwnerUserId = r.OwnerUserId;
        c.OwnerName = r.OwnerName;
        await _db.SaveChangesAsync(ct);
        return (await ListControlsAsync(riskId, ct)).Single(x => x.Id == c.Id);
    }

    public async Task<ControlDto> AssessControlAsync(Guid riskId, Guid controlId, AssessControlRequest r, CancellationToken ct)
    {
        new Validator().OneOf("effectiveness", r.Effectiveness, Effectiveness).ThrowIfInvalid();
        await GetRiskAsync(riskId, ct);
        var c = await _db.RiskControls.SingleOrDefaultAsync(x => x.Id == controlId && x.RiskId == riskId, ct) ?? throw new NotFoundException("Control", controlId);
        c.Effectiveness = r.Effectiveness;
        c.LastAssessedOn = _clock.Today;
        _db.ControlAssessments.Add(new ControlAssessment
        {
            ControlId = controlId, AssessedOn = _clock.Today, Effectiveness = r.Effectiveness, Comment = r.Comment, AssessedBy = _user.DisplayName ?? _user.Username
        });
        await _db.SaveChangesAsync(ct);
        return (await ListControlsAsync(riskId, ct)).Single(x => x.Id == controlId);
    }

    public async Task<IReadOnlyList<TreatmentDto>> ListTreatmentsAsync(Guid riskId, CancellationToken ct)
    {
        await GetRiskAsync(riskId, ct);
        var today = _clock.Today;
        return (await _db.RiskTreatments.AsNoTracking().Where(t => t.RiskId == riskId).OrderBy(t => t.DueDate).ToListAsync(ct))
            .Select(t => ToDto(t, today)).ToList();
    }

    public async Task<TreatmentDto> SaveTreatmentAsync(Guid riskId, Guid? id, SaveTreatmentRequest r, CancellationToken ct)
    {
        new Validator().Required("description", r.Description, 4000).Required("ownerName", r.OwnerName, 200).ThrowIfInvalid();
        var risk = await GetRiskAsync(riskId, ct);
        RiskTreatment t;
        if (id is null)
        {
            t = new RiskTreatment { RiskId = riskId, ProjectId = risk.ProjectId };
            _db.RiskTreatments.Add(t);
            var entity = await _db.Risks.SingleAsync(x => x.Id == riskId, ct);
            if (entity.Status == RiskStatus.Open) entity.Status = RiskStatus.Treating;
        }
        else
        {
            t = await _db.RiskTreatments.SingleOrDefaultAsync(x => x.Id == id && x.RiskId == riskId, ct) ?? throw new NotFoundException("Treatment", id);
        }
        t.Description = r.Description;
        t.OwnerUserId = r.OwnerUserId;
        t.OwnerName = r.OwnerName;
        t.DueDate = r.DueDate;
        if (r.Status is { } s)
        {
            t.Status = s;
            if (s == ActionStatus.Completed) t.CompletedAtUtc ??= _clock.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return ToDto(t, _clock.Today);
    }

    public async Task<HeatMapDto> HeatMapAsync(Guid? projectId, bool residual, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.Risks.AsNoTracking().InScopeNullable(scope, r => r.ProjectId).Where(r => r.Status == RiskStatus.Open || r.Status == RiskStatus.Treating);
        if (projectId is { } pid) q = q.Where(r => r.ProjectId == pid);
        var risks = await q.Select(r => new { L = residual ? r.ResidualLikelihood : r.InherentLikelihood, I = residual ? r.ResidualImpact : r.InherentImpact })
            .ToListAsync(ct);
        var bandDtos = await GetBandsAsync(ct);
        var bands = bandDtos.Select(b => new RatingBand(b.Name, b.MinScore, b.MaxScore, b.Colour)).ToList();
        var cells = new List<HeatCell>();
        for (var l = RiskRating.ScaleMax; l >= 1; l--)
        {
            for (var i = 1; i <= RiskRating.ScaleMax; i++)
            {
                var (score, rating) = RiskRating.Rate(l, i, bands);
                var colour = bands.First(b => b.Name == rating).Colour;
                cells.Add(new HeatCell(l, i, score, rating, colour, risks.Count(r => r.L == l && r.I == i)));
            }
        }
        return new HeatMapDto(residual ? "Residual" : "Inherent", cells, bandDtos);
    }

    // ----- Compliance (FR-RSK-005) -----
    public async Task<IReadOnlyList<ObligationDto>> ListObligationsAsync(CancellationToken ct)
    {
        var obligations = await _db.ComplianceObligations.AsNoTracking().OrderBy(o => o.Code).ToListAsync(ct);
        var latest = (await _db.ComplianceAttestations.AsNoTracking().ToListAsync(ct))
            .GroupBy(a => a.ObligationId).ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AttestedAtUtc).First());
        return obligations.Select(o =>
        {
            latest.TryGetValue(o.Id, out var a);
            return new ObligationDto(o.Id, o.Code, o.Title, o.Source, o.Description, o.Frequency, o.OwnerUserId, o.OwnerName, o.IsActive, a?.Period,
                a?.Result.ToString(), a?.AttestedAtUtc);
        }).ToList();
    }

    public async Task<ObligationDto> SaveObligationAsync(Guid? id, SaveObligationRequest r, CancellationToken ct)
    {
        new Validator().Required("code", r.Code, 40).Required("title", r.Title, 300).Required("source", r.Source, 200).Required("frequency", r.Frequency, 40)
            .Required("ownerName", r.OwnerName, 200).ThrowIfInvalid();
        ComplianceObligation o;
        if (id is null)
        {
            if (await _db.ComplianceObligations.AnyAsync(x => x.Code == r.Code, ct)) throw new ConflictException($"Obligation {r.Code} already exists.");
            o = new ComplianceObligation { Code = r.Code };
            _db.ComplianceObligations.Add(o);
        }
        else
        {
            o = await _db.ComplianceObligations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Obligation", id);
        }
        o.Title = r.Title;
        o.Source = r.Source;
        o.Description = r.Description;
        o.Frequency = r.Frequency;
        o.OwnerUserId = r.OwnerUserId;
        o.OwnerName = r.OwnerName;
        o.IsActive = r.IsActive;
        await _db.SaveChangesAsync(ct);
        return (await ListObligationsAsync(ct)).Single(x => x.Id == o.Id);
    }

    public async Task<IReadOnlyList<AttestationDto>> ListAttestationsAsync(Guid? obligationId, string? period, CancellationToken ct)
    {
        var q = from a in _db.ComplianceAttestations.AsNoTracking()
                join o in _db.ComplianceObligations.AsNoTracking() on a.ObligationId equals o.Id
                select new { a, o.Code };
        if (obligationId is { } oid) q = q.Where(x => x.a.ObligationId == oid);
        if (!string.IsNullOrEmpty(period)) q = q.Where(x => x.a.Period == period);
        return (await q.OrderByDescending(x => x.a.AttestedAtUtc).ToListAsync(ct))
            .Select(x => new AttestationDto(x.a.Id, x.a.ObligationId, x.Code, x.a.Period, x.a.Result.ToString(), x.a.Comment, x.a.AttestedAtUtc, x.a.AttestedBy)).ToList();
    }

    public async Task<AttestationDto> AttestAsync(Guid obligationId, AttestRequest r, CancellationToken ct)
    {
        new Validator().Required("period", r.Period, 20)
            .Must(r.Result is AttestationResult.Compliant or AttestationResult.NotApplicable || !string.IsNullOrWhiteSpace(r.Comment), "comment",
                "Explain partial or non-compliance.").ThrowIfInvalid();
        var o = await _db.ComplianceObligations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == obligationId, ct) ?? throw new NotFoundException("Obligation", obligationId);
        if (await _db.ComplianceAttestations.AnyAsync(a => a.ObligationId == obligationId && a.Period == r.Period, ct))
            throw new ConflictException($"{o.Code} has already been attested for {r.Period}.");
        var a = new ComplianceAttestation
        {
            ObligationId = obligationId, Period = r.Period, Result = r.Result, Comment = r.Comment, AttestedAtUtc = _clock.UtcNow,
            AttestedBy = _user.DisplayName ?? _user.Username ?? "?", AttestedByUserId = _user.UserId
        };
        _db.ComplianceAttestations.Add(a);
        await _db.SaveChangesAsync(ct);
        return new AttestationDto(a.Id, a.ObligationId, o.Code, a.Period, a.Result.ToString(), a.Comment, a.AttestedAtUtc, a.AttestedBy);
    }

    // ----- Audit findings (FR-RSK-006) -----
    public async Task<IReadOnlyList<AuditFindingDto>> ListAuditFindingsAsync(Guid? projectId, bool openOnly, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.AuditFindings.AsNoTracking().InScopeNullable(scope, f => f.ProjectId);
        if (projectId is { } pid) q = q.Where(f => f.ProjectId == pid);
        if (openOnly) q = q.Where(f => f.Status != FindingStatus.Closed);
        return await ToDtosAsync(await q.OrderBy(f => f.DueDate).ToListAsync(ct), ct);
    }

    public async Task<AuditFindingDto> SaveAuditFindingAsync(Guid? id, SaveAuditFindingRequest r, CancellationToken ct)
    {
        new Validator().Required("process", r.Process, 200).Required("title", r.Title, 300).Required("description", r.Description, 8000)
            .Required("actionOwnerName", r.ActionOwnerName, 200).Must(r.DueDate != default, "dueDate", "A due date is required.").ThrowIfInvalid();
        if (r.ProjectId is { } pid) await _scope.EnsureProjectAsync(pid, ct);
        AuditFinding f;
        if (id is null)
        {
            f = new AuditFinding { Number = await _numbers.NextAsync(NumberPrefixes.AuditFinding, ct) };
            _db.AuditFindings.Add(f);
        }
        else
        {
            f = await _db.AuditFindings.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Audit finding", id);
            if (f.Status == FindingStatus.Closed) throw new DomainException("Closed findings are read-only.", "FR-RSK-006");
        }
        f.ProjectId = r.ProjectId;
        f.Process = r.Process;
        f.Source = r.Source;
        f.AuditReference = r.AuditReference;
        f.Title = r.Title;
        f.Description = r.Description;
        f.Recommendation = r.Recommendation;
        f.Rating = r.Rating;
        f.ManagementResponse = r.ManagementResponse;
        f.ActionOwnerUserId = r.ActionOwnerUserId;
        f.ActionOwnerName = r.ActionOwnerName;
        f.DueDate = r.DueDate;
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { f }, ct))[0];
    }

    // ----- Combined assurance (FR-RSK-007) -----
    public async Task<CoverageMapDto> CoverageMapAsync(string period, CancellationToken ct)
    {
        var entries = await _db.AssuranceCoverage.AsNoTracking().Where(c => c.Period == period).ToListAsync(ct);
        var lines = Enum.GetNames<AssuranceLine>();
        var rows = entries.GroupBy(e => e.Area).OrderBy(g => g.Key).Select(g =>
        {
            var map = lines.ToDictionary(l => l, l => g.Any(e => e.Line.ToString() == l && e.Covered));
            // Gap: no independent (internal audit or external) assurance, or no management assurance.
            var gap = !map[nameof(AssuranceLine.Management)] || (!map[nameof(AssuranceLine.InternalAudit)] && !map[nameof(AssuranceLine.ExternalAssurance)]);
            return new CoverageRow(g.Key, map, gap);
        }).ToList();
        return new CoverageMapDto(period, lines, rows, rows.Count(r => r.HasGap));
    }

    public async Task<CoverageDto> SaveCoverageAsync(Guid? id, SaveCoverageRequest r, CancellationToken ct)
    {
        new Validator().Required("area", r.Area, 200).Required("period", r.Period, 20).ThrowIfInvalid();
        AssuranceCoverage c;
        if (id is null)
        {
            c = await _db.AssuranceCoverage.SingleOrDefaultAsync(x => x.Area == r.Area && x.Line == r.Line && x.Period == r.Period, ct) ?? new AssuranceCoverage();
            if (c.Id == Guid.Empty || _db.AssuranceCoverage.Local.All(x => x.Id != c.Id)) _db.AssuranceCoverage.Add(c);
        }
        else
        {
            c = await _db.AssuranceCoverage.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Coverage", id);
        }
        c.Area = r.Area;
        c.RiskId = r.RiskId;
        c.Line = r.Line;
        c.Period = r.Period;
        c.Covered = r.Covered;
        c.Provider = r.Provider;
        c.Rating = r.Rating;
        c.Comments = r.Comments;
        await _db.SaveChangesAsync(ct);
        return new CoverageDto(c.Id, c.Area, c.RiskId, c.Line.ToString(), c.Period, c.Covered, c.Provider, c.Rating, c.Comments);
    }

    // ----- Dashboard (FR-RSK-008) -----
    public async Task<AssuranceDashboardDto> DashboardAsync(Guid? projectId, CancellationToken ct)
    {
        var risks = await ListRisksAsync(projectId, null, null, null, true, ct);
        var findings = await ListAuditFindingsAsync(projectId, true, ct);
        var today = _clock.Today;
        var scope = await _scope.GetAsync(ct);
        var auditActionQuery = _db.CorrectiveActions.AsNoTracking().InScopeNullable(scope, a => a.ProjectId)
            .Where(a => a.ParentType == ParentTypes.AuditFinding && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress));
        if (projectId is { } pid) auditActionQuery = auditActionQuery.Where(a => a.ProjectId == pid);
        var overdueAuditActions = (await auditActionQuery.Select(a => a.DueDate).ToListAsync(ct)).Count(d => d < today);
        var nonCompliant = (await ListObligationsAsync(ct)).Count(o => o.LastResult is nameof(AttestationResult.NonCompliant) or nameof(AttestationResult.PartiallyCompliant));
        var period = Fy.For(today);
        var coverage = await CoverageMapAsync(period, ct);
        return new AssuranceDashboardDto(risks.Count, risks.Count(r => r.ResidualRating == "Critical"), risks.Count(r => r.ResidualRating == "High"),
            risks.Sum(r => r.OverdueTreatments), risks.Count(r => r.ReviewOverdue), findings.Count, overdueAuditActions, nonCompliant, coverage.Gaps,
            await HeatMapAsync(projectId, true, ct), risks.Take(10).ToList(), findings.Where(f => f.IsOverdue).Take(10).ToList());
    }

    // ----- helpers -----
    private static TreatmentDto ToDto(RiskTreatment t, DateOnly today) =>
        new(t.Id, t.RiskId, t.Description, t.OwnerUserId, t.OwnerName, t.DueDate, t.Status.ToString(), t.CompletedAtUtc, t.EscalationLevel,
            t.Status is ActionStatus.Open or ActionStatus.InProgress && t.DueDate < today);

    private async Task<List<RiskDto>> ToDtosAsync(IReadOnlyCollection<Risk> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var projectIds = rows.Where(r => r.ProjectId != null).Select(r => r.ProjectId!.Value).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = p.ProjectNumber ?? p.DraftReference }).ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        var treatments = await _db.RiskTreatments.AsNoTracking().Where(t => ids.Contains(t.RiskId)
            && (t.Status == ActionStatus.Open || t.Status == ActionStatus.InProgress)).Select(t => new { t.RiskId, t.DueDate }).ToListAsync(ct);
        var today = _clock.Today;
        return rows.Select(r => new RiskDto(r.Id, r.Number, r.ParentType, r.ParentId, r.ProjectId, r.ProjectId is { } pid ? projects.GetValueOrDefault(pid) : null,
            r.Title, r.Cause, r.Event, r.Consequence, r.Category, r.InherentLikelihood, r.InherentImpact, r.InherentScore, r.InherentRating,
            r.ResidualLikelihood, r.ResidualImpact, r.ResidualScore, r.ResidualRating, r.OwnerUserId, r.OwnerName, r.ReviewDate, r.Status.ToString(),
            r.IsOpen && r.ReviewDate < today, treatments.Count(t => t.RiskId == r.Id), treatments.Count(t => t.RiskId == r.Id && t.DueDate < today),
            r.Version)).ToList();
    }

    private async Task<List<AuditFindingDto>> ToDtosAsync(IReadOnlyCollection<AuditFinding> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var projectIds = rows.Where(r => r.ProjectId != null).Select(r => r.ProjectId!.Value).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = p.ProjectNumber ?? p.DraftReference }).ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        var actions = await _db.CorrectiveActions.AsNoTracking().Where(a => a.ParentType == ParentTypes.AuditFinding && ids.Contains(a.ParentId)
            && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress)).Select(a => a.ParentId).ToListAsync(ct);
        var today = _clock.Today;
        return rows.Select(f => new AuditFindingDto(f.Id, f.Number, f.ProjectId, f.ProjectId is { } pid ? projects.GetValueOrDefault(pid) : null, f.Process,
            f.Source.ToString(), f.AuditReference, f.Title, f.Description, f.Recommendation, f.Rating.ToString(), f.ManagementResponse, f.ActionOwnerUserId,
            f.ActionOwnerName, f.DueDate, f.Status.ToString(), f.Status != FindingStatus.Closed && f.DueDate < today, actions.Count(a => a == f.Id),
            f.ClosedAtUtc, f.Version)).ToList();
    }
}
