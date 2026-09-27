using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Budget;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Engines;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Budget;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Scm;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Suppliers;
using Teta.Ippcms.Domain.Workflow;
using ProcurementEntity = Teta.Ippcms.Domain.Scm.Procurement;

namespace Teta.Ippcms.Application.Sourcing;

public interface IProcurementService
{
    // Requisitions
    Task<IReadOnlyList<RequisitionDto>> ListRequisitionsAsync(Guid? projectId, string? status, CancellationToken ct);
    Task<RequisitionDto> GetRequisitionAsync(Guid id, CancellationToken ct);
    Task<RequisitionDto> SaveRequisitionAsync(Guid? id, SaveRequisitionRequest request, CancellationToken ct);
    Task<WorkflowInstanceDto> SubmitRequisitionAsync(Guid id, CancellationToken ct);
    Task<ProcurementDetailDto> ConvertToProcurementAsync(Guid requisitionId, CancellationToken ct);

    // Procurements
    Task<PagedResult<ProcurementListItemDto>> ListAsync(ProcurementQuery query, CancellationToken ct);
    Task<ProcurementDetailDto> GetAsync(Guid id, CancellationToken ct);
    Task<SpecificationDto> SaveSpecificationAsync(Guid procurementId, SaveSpecificationRequest request, CancellationToken ct);
    Task<SpecificationDto> SubmitSpecificationAsync(Guid procurementId, Guid specificationId, CancellationToken ct);
    Task<SpecificationDto> ApproveSpecificationAsync(Guid procurementId, Guid specificationId, CancellationToken ct);
    Task<CommitteeDto> SaveCommitteeAsync(Guid procurementId, SaveCommitteeRequest request, CancellationToken ct);
    Task<CommitteeDto> AddMemberAsync(Guid committeeId, AddMemberRequest request, CancellationToken ct);
    Task<CommitteeDto> EndMembershipAsync(Guid committeeId, Guid memberId, CancellationToken ct);
    Task<IReadOnlyList<MeetingDto>> ListMeetingsAsync(Guid committeeId, CancellationToken ct);
    Task<MeetingDto> AddMeetingAsync(Guid committeeId, SaveMeetingRequest request, CancellationToken ct);
    Task<IReadOnlyList<DeclarationDto>> ListDeclarationsAsync(Guid procurementId, CancellationToken ct);
    Task<DeclarationDto> DeclareAsync(Guid procurementId, DeclareRequest request, CancellationToken ct);
    Task<PublicationDto> PublishAsync(Guid procurementId, SavePublicationRequest request, CancellationToken ct);
    Task<IReadOnlyList<BidDto>> ListBidsAsync(Guid procurementId, CancellationToken ct);
    Task<BidDto> RegisterBidAsync(Guid procurementId, RegisterBidRequest request, CancellationToken ct);
    Task<IReadOnlyList<BidDto>> OpenBidsAsync(Guid procurementId, CancellationToken ct);
    Task<BidDto> InvalidateBidAsync(Guid procurementId, Guid bidId, string reason, CancellationToken ct);
    Task<CriterionDto> SaveCriterionAsync(Guid procurementId, Guid? id, SaveCriterionRequest request, CancellationToken ct);
    Task DeleteCriterionAsync(Guid procurementId, Guid criterionId, CancellationToken ct);
    Task SetupEvaluationAsync(Guid procurementId, EvaluationSetupRequest request, CancellationToken ct);
    Task<IReadOnlyList<ScoreDto>> MyScoresAsync(Guid procurementId, CancellationToken ct);
    Task<ScoreDto> SubmitScoreAsync(Guid procurementId, SubmitScoreRequest request, CancellationToken ct);
    Task<EvaluationReportDto> ConsolidateAsync(Guid procurementId, CancellationToken ct);
    Task<EvaluationReportDto> EvaluationReportAsync(Guid procurementId, CancellationToken ct);
    Task<DueDiligenceDto> SaveDueDiligenceAsync(Guid procurementId, SaveDueDiligenceRequest request, CancellationToken ct);
    Task<IReadOnlyList<DueDiligenceDto>> ListDueDiligenceAsync(Guid procurementId, CancellationToken ct);
    Task<WorkflowInstanceDto> SubmitAdjudicationAsync(Guid procurementId, SubmitAdjudicationRequest request, CancellationToken ct);
    Task<AwardDto> SatisfyAwardConditionsAsync(Guid awardId, string note, CancellationToken ct);
    Task<IReadOnlyList<AwardDto>> ListAwardsAsync(bool pendingContractOnly, CancellationToken ct);
    Task<ProcurementDetailDto> CancelAsync(Guid procurementId, string reason, CancellationToken ct);
    Task<ProcurementDetailDto> ReAdvertiseAsync(Guid procurementId, string reason, CancellationToken ct);
    Task<IReadOnlyList<CommunicationDto>> ListCommunicationsAsync(Guid procurementId, CancellationToken ct);
    Task<CommunicationDto> AddCommunicationAsync(Guid procurementId, SaveCommunicationRequest request, CancellationToken ct);

    // Exceptions/deviations
    Task<IReadOnlyList<ExceptionDto>> ListExceptionsAsync(string? status, CancellationToken ct);
    Task<ExceptionDto> SaveExceptionAsync(Guid? id, SaveExceptionRequest request, CancellationToken ct);
    Task<WorkflowInstanceDto> SubmitExceptionAsync(Guid id, CancellationToken ct);

    // Reporting
    Task<IReadOnlyList<TransparencyRow>> TransparencyAsync(DateOnly? from, DateOnly? to, CancellationToken ct);
    Task<ProcurementDashboardDto> DashboardAsync(Guid? projectId, string? orgUnit, string? method, string? status, CancellationToken ct);
}

/// <summary>Procurement and bid management (SRS §5.4), BR-002/BR-003/BR-004, SRS §28.</summary>
public sealed class ProcurementService : IProcurementService
{
    public const string RequisitionWorkflow = "REQUISITION_APPROVAL";
    public const string AdjudicationWorkflow = "PROCUREMENT_ADJUDICATION";
    public const string ExceptionWorkflow = "PROCUREMENT_EXCEPTION";
    public const string ProcurementEntityName = "Procurement";

    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAccessScope _scope;
    private readonly INumberGenerator _numbers;
    private readonly IWorkflowService _workflow;
    private readonly IBudgetService _budget;
    private readonly ITransactionLedger _ledger;
    private readonly ISodService _sod;
    private readonly IAuditWriter _audit;

    public ProcurementService(ITetaDbContext db, ICurrentUser user, IClock clock, IAccessScope scope, INumberGenerator numbers,
        IWorkflowService workflow, IBudgetService budget, ITransactionLedger ledger, ISodService sod, IAuditWriter audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _scope = scope;
        _numbers = numbers;
        _workflow = workflow;
        _budget = budget;
        _ledger = ledger;
        _sod = sod;
        _audit = audit;
    }

    // =================== Requisitions ===================
    public async Task<IReadOnlyList<RequisitionDto>> ListRequisitionsAsync(Guid? projectId, string? status, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.Requisitions.AsNoTracking().InScope(scope, r => r.ProjectId);
        if (projectId is { } pid) q = q.Where(r => r.ProjectId == pid);
        if (Enum.TryParse<RequisitionStatus>(status, true, out var st)) q = q.Where(r => r.Status == st);
        var rows = await q.OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct);
        return await ToDtosAsync(rows, ct);
    }

    public async Task<RequisitionDto> GetRequisitionAsync(Guid id, CancellationToken ct)
    {
        var r = await _db.Requisitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Requisition", id);
        await _scope.EnsureProjectAsync(r.ProjectId, ct);
        return (await ToDtosAsync(new[] { r }, ct))[0];
    }

    public async Task<RequisitionDto> SaveRequisitionAsync(Guid? id, SaveRequisitionRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("projectId", r.ProjectId).Required("title", r.Title, 300).Optional("description", r.Description)
            .NonNegative("estimatedValue", r.EstimatedValue).Optional("methodJustification", r.MethodJustification, 2000).ThrowIfInvalid();
        await _scope.EnsureProjectAsync(r.ProjectId, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == r.ProjectId, ct);
        project.EnsureActive();   // BR-002: must reference an approved project
        if (r.BudgetLineId is { } bl && !await _db.BudgetLines.AnyAsync(b => b.Id == bl && b.ProjectId == r.ProjectId, ct))
            throw new ValidationException("budgetLineId", "The budget line does not belong to this project.");
        if (r.PlanItemId is { } pi && !await _db.ProcurementPlanItems.AnyAsync(i => i.Id == pi && i.ProjectId == r.ProjectId, ct))
            throw new ValidationException("planItemId", "The procurement plan item does not belong to this project.");

        Requisition req;
        if (id is null)
        {
            req = new Requisition { Number = await _numbers.NextAsync(NumberPrefixes.Requisition, ct), ProjectId = r.ProjectId };
            _db.Requisitions.Add(req);
            _ledger.Record(nameof(Requisition), req.Id, "CreateRequisition", r.ProjectId);
        }
        else
        {
            req = await _db.Requisitions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Requisition", id);
            if (req.Status != RequisitionStatus.Draft) throw new DomainException($"Requisition {req.Number} is {req.Status} and cannot be edited.", "FR-SCM-001");
        }
        req.PlanItemId = r.PlanItemId;
        req.BudgetLineId = r.BudgetLineId;
        req.Title = r.Title.Trim();
        req.Description = r.Description;
        req.EstimatedValue = r.EstimatedValue;
        req.RequiredByDate = r.RequiredByDate;
        req.SelectedMethod = r.SelectedMethod;
        req.MethodJustification = r.MethodJustification;
        req.ExceptionId = r.ExceptionId;
        await _db.SaveChangesAsync(ct);
        return await GetRequisitionAsync(req.Id, ct);
    }

    public async Task<WorkflowInstanceDto> SubmitRequisitionAsync(Guid id, CancellationToken ct)
    {
        var req = await _db.Requisitions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Requisition", id);
        await _scope.EnsureProjectAsync(req.ProjectId, ct);
        if (req.Status != RequisitionStatus.Draft) throw new DomainException($"Requisition {req.Number} is {req.Status}.", "FR-SCM-001");

        var missing = req.MissingMandatoryFields();
        if (missing.Count > 0)
            throw new ValidationException(missing.ToDictionary(m => char.ToLowerInvariant(m[0]) + m[1..], _ => new[] { "Required before submission." }));

        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == req.ProjectId, ct);
        project.EnsureActive();

        // FR-BUD-005 / BR-002: confirmed budget unless an approved exception applies.
        var availability = await _budget.CheckAvailabilityAsync(req.ProjectId, req.BudgetLineId, req.EstimatedValue, req.Id, ct);
        req.BudgetAvailable = availability.Available;
        req.BudgetCheckResult = availability.Message;
        if (!availability.Available)
        {
            var exceptionApproved = req.ExceptionId is { } exId && await _db.ProcurementExceptions.AnyAsync(e => e.Id == exId
                && e.Status == ApprovalState.Approved && e.Type == ExceptionType.BudgetException, ct);
            if (!exceptionApproved)
            {
                await _db.SaveChangesAsync(ct);
                throw new DomainException(availability.Message + " Route an approved budget exception to proceed.", "FR-BUD-005");
            }
            _audit.Write("Procurement", nameof(Requisition), req.Id.ToString(), "BudgetExceptionApplied",
                new { req.ExceptionId, availability.Remaining, req.EstimatedValue }, "Approved budget exception");
        }

        // FR-BUD-007: method from configurable, effective-dated policy rules, with justification on override.
        var selection = await _budget.SelectMethodAsync(req.EstimatedValue, _clock.Today, ct);
        req.RecommendedMethod = selection.MethodCode;
        req.MethodRuleId = selection.RuleId;
        req.MethodRuleVersion = selection.RuleVersion;
        if (string.IsNullOrWhiteSpace(req.SelectedMethod)) req.SelectedMethod = selection.MethodCode;
        if (!string.Equals(req.SelectedMethod, selection.MethodCode, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(req.MethodJustification))
            throw new ValidationException("methodJustification", $"Justify selecting {req.SelectedMethod} instead of the rule-based method {selection.MethodCode}.");
        req.MethodJustification ??= selection.Rationale;

        req.Status = RequisitionStatus.Submitted;
        if (req.PlanItemId is { } planItemId)
        {
            var item = await _db.ProcurementPlanItems.SingleAsync(i => i.Id == planItemId, ct);
            item.ActualRequisitionDate ??= _clock.Today;
            if (item.Status == PlanItemStatus.Planned) item.Status = PlanItemStatus.InProgress;
        }

        var instance = await _workflow.StartAsync(new StartWorkflowRequest(RequisitionWorkflow, nameof(Requisition), req.Id, req.Number,
            $"Requisition: {req.Title}", req.EstimatedValue, req.ProjectId, $"procurement/requisitions/{req.Id}"), ct);
        await _db.SaveChangesAsync(ct);
        return await _workflow.GetAsync(instance.Id, ct);
    }

    public async Task<ProcurementDetailDto> ConvertToProcurementAsync(Guid requisitionId, CancellationToken ct)
    {
        var req = await _db.Requisitions.SingleOrDefaultAsync(x => x.Id == requisitionId, ct) ?? throw new NotFoundException("Requisition", requisitionId);
        await _scope.EnsureProjectAsync(req.ProjectId, ct);
        if (req.Status != RequisitionStatus.Approved) throw new DomainException("Only an approved requisition can start a procurement.", "BR-002");
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == req.ProjectId, ct);

        var procurement = new ProcurementEntity
        {
            Number = await _numbers.NextAsync(NumberPrefixes.Procurement, ct),
            ProjectId = req.ProjectId, RequisitionId = req.Id, Title = req.Title, Method = req.SelectedMethod ?? req.RecommendedMethod ?? "RFQ",
            MethodRuleId = req.MethodRuleId, MethodRuleVersion = req.MethodRuleVersion, EstimatedValue = req.EstimatedValue, OrgUnit = project.OrgUnit
        };
        if (req.PlanItemId is { } pi)
        {
            var item = await _db.ProcurementPlanItems.AsNoTracking().SingleAsync(i => i.Id == pi, ct);
            procurement.PlannedAwardDate = item.PlannedAwardDate;
        }
        _db.Procurements.Add(procurement);
        req.Status = RequisitionStatus.Converted;
        req.ProcurementId = procurement.Id;
        _db.Specifications.Add(new Specification
        {
            ProcurementId = procurement.Id, Title = req.Title, Content = req.Description ?? string.Empty
        });
        await _db.SaveChangesAsync(ct);
        return await GetAsync(procurement.Id, ct);
    }

    // =================== Procurements ===================
    public async Task<PagedResult<ProcurementListItemDto>> ListAsync(ProcurementQuery q, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var query = _db.Procurements.AsNoTracking().InScope(scope, p => p.ProjectId);
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(p => p.Title.Contains(q.Search) || p.Number.Contains(q.Search));
        if (q.ProjectId is { } pid) query = query.Where(p => p.ProjectId == pid);
        if (Enum.TryParse<ProcurementStatus>(q.Status, true, out var st)) query = query.Where(p => p.Status == st);
        if (!string.IsNullOrWhiteSpace(q.Method)) query = query.Where(p => p.Method == q.Method);
        if (!string.IsNullOrWhiteSpace(q.OrgUnit)) query = query.Where(p => p.OrgUnit == q.OrgUnit);
        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 500);
        var rows = await query.OrderByDescending(p => p.CreatedAtUtc).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<ProcurementListItemDto>(await ToListItemsAsync(rows, ct), total, page, size);
    }

    public async Task<ProcurementDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var p = await LoadAsync(id, ct, tracking: false);
        var summary = (await ToListItemsAsync(new[] { p }, ct))[0];
        var req = p.RequisitionId is { } rid ? await _db.Requisitions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == rid, ct) : null;
        var specs = await _db.Specifications.AsNoTracking().Where(s => s.ProcurementId == id).OrderByDescending(s => s.VersionNumber).ToListAsync(ct);
        var committees = await CommitteesAsync(id, ct);
        var publications = await _db.Publications.AsNoTracking().Where(x => x.ProcurementId == id).OrderBy(x => x.PublishedOn)
            .Select(x => new PublicationDto(x.Id, x.Channel, x.Reference, x.PublishedOn, x.ClosingDateUtc, x.Url, x.BriefingSession, x.EvidenceDocumentId))
            .ToListAsync(ct);
        var criteria = await CriteriaAsync(id, ct);
        var adjudication = await _db.Adjudications.AsNoTracking().Where(a => a.ProcurementId == id).OrderByDescending(a => a.CreatedAtUtc).FirstOrDefaultAsync(ct);
        var award = await _db.Awards.AsNoTracking().Where(a => a.ProcurementId == id && a.Status != AwardStatus.Cancelled).FirstOrDefaultAsync(ct);
        var rule = p.MethodRuleId is { } mr ? await _db.ProcurementMethodRules.AsNoTracking().SingleOrDefaultAsync(r => r.Id == mr, ct) : null;
        var (canAccess, isEvaluator, declared) = await AccessAsync(id, ct);

        return new ProcurementDetailDto(summary, p.RequisitionId, req?.Number, p.TechnicalThreshold, p.PriceSystemCode, p.CancellationReason,
            p.ReAdvertisedFromId, p.ReAdvertisedAsId, rule is null ? null : $"{rule.Name} v{rule.RuleVersion} (effective {rule.EffectiveFrom:yyyy-MM-dd})",
            canAccess, isEvaluator, declared,
            specs.Select(ToDto).ToList(), committees, publications, criteria,
            adjudication is null ? null : await ToDtoAsync(adjudication, ct),
            award is null ? null : await ToDtoAsync(award, ct), p.Version);
    }

    // ----- Specification / TOR (FR-SCM-002) -----
    public async Task<SpecificationDto> SaveSpecificationAsync(Guid procurementId, SaveSpecificationRequest r, CancellationToken ct)
    {
        new Validator().Required("title", r.Title, 300).Required("content", r.Content, 200000).ThrowIfInvalid();
        var p = await LoadAsync(procurementId, ct);
        p.EnsureNotTerminal();
        if (p.Status is not (ProcurementStatus.Planning or ProcurementStatus.SpecificationApproved))
            throw new DomainException("The specification cannot change once the procurement is published.", "FR-SCM-002");
        var latest = await _db.Specifications.Where(s => s.ProcurementId == procurementId).OrderByDescending(s => s.VersionNumber).FirstOrDefaultAsync(ct);
        Specification spec;
        if (latest is null || latest.Status is SpecificationStatus.Approved or SpecificationStatus.Superseded)
        {
            spec = new Specification { ProcurementId = procurementId, VersionNumber = (latest?.VersionNumber ?? 0) + 1 };
            _db.Specifications.Add(spec);
        }
        else
        {
            spec = latest;
            spec.EnsureEditable();
            if (spec.Status == SpecificationStatus.UnderReview) spec.Status = SpecificationStatus.Draft;
        }
        spec.Title = r.Title;
        spec.Content = r.Content;
        _ledger.Record(nameof(Specification), spec.Id, "Author", p.ProjectId);
        await _db.SaveChangesAsync(ct);
        return ToDto(spec);
    }

    public async Task<SpecificationDto> SubmitSpecificationAsync(Guid procurementId, Guid specificationId, CancellationToken ct)
    {
        await LoadAsync(procurementId, ct);
        var spec = await _db.Specifications.SingleOrDefaultAsync(s => s.Id == specificationId && s.ProcurementId == procurementId, ct)
                   ?? throw new NotFoundException("Specification", specificationId);
        if (spec.Status != SpecificationStatus.Draft) throw new DomainException("Only a draft specification can be submitted for review.", "FR-SCM-002");
        spec.Status = SpecificationStatus.UnderReview;
        await _db.SaveChangesAsync(ct);
        return ToDto(spec);
    }

    public async Task<SpecificationDto> ApproveSpecificationAsync(Guid procurementId, Guid specificationId, CancellationToken ct)
    {
        var p = await LoadAsync(procurementId, ct);
        var spec = await _db.Specifications.SingleOrDefaultAsync(s => s.Id == specificationId && s.ProcurementId == procurementId, ct)
                   ?? throw new NotFoundException("Specification", specificationId);
        if (spec.Status != SpecificationStatus.UnderReview) throw new DomainException("Only a specification under review can be approved.", "FR-SCM-002");
        var userId = _user.UserId ?? throw new ForbiddenException();
        await _sod.EnsureAllowedAsync(nameof(Specification), spec.Id, "Approve", userId, ct);

        foreach (var previous in await _db.Specifications.Where(s => s.ProcurementId == procurementId && s.Status == SpecificationStatus.Approved).ToListAsync(ct))
            previous.Status = SpecificationStatus.Superseded;
        spec.Status = SpecificationStatus.Approved;
        spec.ApprovedAtUtc = _clock.UtcNow;
        spec.ApprovedBy = _user.DisplayName ?? _user.Username;
        if (p.Status == ProcurementStatus.Planning) p.Status = ProcurementStatus.SpecificationApproved;
        _ledger.Record(nameof(Specification), spec.Id, "Approve", p.ProjectId);
        await _db.SaveChangesAsync(ct);
        return ToDto(spec);
    }

    // ----- Committees (FR-SCM-003) -----
    public async Task<CommitteeDto> SaveCommitteeAsync(Guid procurementId, SaveCommitteeRequest r, CancellationToken ct)
    {
        new Validator().Required("name", r.Name, 200).Range("quorum", r.Quorum, 1, 20).ThrowIfInvalid();
        var p = await LoadAsync(procurementId, ct);
        p.EnsureNotTerminal();
        var committee = await _db.Committees.SingleOrDefaultAsync(c => c.ProcurementId == procurementId && c.Type == r.Type, ct);
        if (committee is null)
        {
            committee = new Committee { ProcurementId = procurementId, Type = r.Type };
            _db.Committees.Add(committee);
        }
        committee.Name = r.Name;
        committee.Quorum = r.Quorum;
        await _db.SaveChangesAsync(ct);
        return (await CommitteesAsync(procurementId, ct)).Single(c => c.Id == committee.Id);
    }

    public async Task<CommitteeDto> AddMemberAsync(Guid committeeId, AddMemberRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("userId", r.UserId).DateOrder("accessFrom", r.AccessFrom, "accessTo", r.AccessTo).ThrowIfInvalid();
        var committee = await _db.Committees.SingleOrDefaultAsync(c => c.Id == committeeId, ct) ?? throw new NotFoundException("Committee", committeeId);
        if (committee.ProcurementId is { } pid) (await LoadAsync(pid, ct)).EnsureNotTerminal();
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == r.UserId && u.IsActive, ct)
                   ?? throw new ValidationException("userId", "User not found or inactive.");
        if (await _db.CommitteeMembers.AnyAsync(m => m.CommitteeId == committeeId && m.UserId == r.UserId, ct))
            throw new ConflictException($"{user.DisplayName} is already a member of this committee.");

        // SoD: the same person should not sit on both the evaluation and adjudication committees.
        if (committee.ProcurementId is { } procurementId)
        {
            var otherType = committee.Type == CommitteeType.BidEvaluation ? CommitteeType.BidAdjudication
                : committee.Type == CommitteeType.BidAdjudication ? CommitteeType.BidEvaluation : (CommitteeType?)null;
            if (otherType is not null)
            {
                var conflict = await (from m in _db.CommitteeMembers
                                      join c in _db.Committees on m.CommitteeId equals c.Id
                                      where c.ProcurementId == procurementId && c.Type == otherType && m.UserId == r.UserId
                                      select m).AnyAsync(ct);
                if (conflict)
                    throw new DomainException("Segregation of duties: a member of the evaluation committee cannot serve on the adjudication committee for the same bid.", "SEC-005");
            }
        }

        _db.CommitteeMembers.Add(new CommitteeMember
        {
            CommitteeId = committeeId, UserId = r.UserId, Name = user.DisplayName, Role = r.Role, AccessFrom = r.AccessFrom, AccessTo = r.AccessTo
        });
        await _db.SaveChangesAsync(ct);
        return (await CommitteesAsync(committee.ProcurementId, ct)).Single(c => c.Id == committeeId);
    }

    public async Task<CommitteeDto> EndMembershipAsync(Guid committeeId, Guid memberId, CancellationToken ct)
    {
        var member = await _db.CommitteeMembers.SingleOrDefaultAsync(m => m.Id == memberId && m.CommitteeId == committeeId, ct)
                     ?? throw new NotFoundException("Committee member", memberId);
        var yesterday = _clock.Today.AddDays(-1);
        member.AccessTo = yesterday < member.AccessFrom ? member.AccessFrom : yesterday;
        await _db.SaveChangesAsync(ct);
        var committee = await _db.Committees.AsNoTracking().SingleAsync(c => c.Id == committeeId, ct);
        return (await CommitteesAsync(committee.ProcurementId, ct)).Single(c => c.Id == committeeId);
    }

    public async Task<IReadOnlyList<MeetingDto>> ListMeetingsAsync(Guid committeeId, CancellationToken ct) =>
        await _db.CommitteeMeetings.AsNoTracking().Where(m => m.CommitteeId == committeeId).OrderByDescending(m => m.MeetingAtUtc)
            .Select(m => new MeetingDto(m.Id, m.CommitteeId, m.MeetingAtUtc, m.Agenda, m.Minutes, m.AttendeesCount, m.QuorumMet, m.Resolutions))
            .ToListAsync(ct);

    public async Task<MeetingDto> AddMeetingAsync(Guid committeeId, SaveMeetingRequest r, CancellationToken ct)
    {
        new Validator().Required("agenda", r.Agenda, 4000).Range("attendeesCount", r.AttendeesCount, 0, 100).ThrowIfInvalid();
        var committee = await _db.Committees.AsNoTracking().SingleOrDefaultAsync(c => c.Id == committeeId, ct) ?? throw new NotFoundException("Committee", committeeId);
        var meeting = new CommitteeMeeting
        {
            CommitteeId = committeeId, MeetingAtUtc = DateTime.SpecifyKind(r.MeetingAtUtc, DateTimeKind.Utc), Agenda = r.Agenda, Minutes = r.Minutes,
            AttendeesCount = r.AttendeesCount, QuorumMet = r.AttendeesCount >= committee.Quorum, Resolutions = r.Resolutions
        };
        _db.CommitteeMeetings.Add(meeting);
        await _db.SaveChangesAsync(ct);
        return new MeetingDto(meeting.Id, committeeId, meeting.MeetingAtUtc, meeting.Agenda, meeting.Minutes, meeting.AttendeesCount, meeting.QuorumMet, meeting.Resolutions);
    }

    // ----- Declarations (FR-SCM-004, BR-003) -----
    public async Task<IReadOnlyList<DeclarationDto>> ListDeclarationsAsync(Guid procurementId, CancellationToken ct)
    {
        await LoadAsync(procurementId, ct, tracking: false);
        return await _db.Declarations.AsNoTracking().Where(d => d.ProcurementId == procurementId).OrderBy(d => d.DeclaredAtUtc)
            .Select(d => new DeclarationDto(d.Id, d.UserId, d.DeclarantName, d.HasConflict, d.ConflictDetails, d.ConfidentialityAccepted, d.DeclaredAtUtc))
            .ToListAsync(ct);
    }

    public async Task<DeclarationDto> DeclareAsync(Guid procurementId, DeclareRequest r, CancellationToken ct)
    {
        var userId = _user.UserId ?? throw new ForbiddenException();
        new Validator().Must(r.ConfidentialityAccepted, "confidentialityAccepted", "The confidentiality undertaking must be accepted.")
            .Must(!r.HasConflict || !string.IsNullOrWhiteSpace(r.ConflictDetails), "conflictDetails", "Describe the conflict of interest.").ThrowIfInvalid();
        _ = await _db.Procurements.AsNoTracking().SingleOrDefaultAsync(p => p.Id == procurementId, ct) ?? throw new NotFoundException("Procurement", procurementId);
        if (!await IsCommitteeMemberAsync(procurementId, userId, ct) && !_user.HasPermission(Permissions.ProcurementManage))
            throw new ForbiddenException("Only committee members and SCM officials declare for this procurement.");
        if (await _db.Declarations.AnyAsync(d => d.ProcurementId == procurementId && d.UserId == userId, ct))
            throw new ConflictException("You have already declared for this procurement. Declarations cannot be changed.");

        var d = new Declaration
        {
            ProcurementId = procurementId, UserId = userId, DeclarantName = _user.DisplayName ?? _user.Username ?? "?", HasConflict = r.HasConflict,
            ConflictDetails = r.ConflictDetails, ConfidentialityAccepted = r.ConfidentialityAccepted, DeclaredAtUtc = _clock.UtcNow
        };
        _db.Declarations.Add(d);
        if (r.HasConflict)
            _audit.Write("Procurement", ProcurementEntityName, procurementId.ToString(), "ConflictDeclared", new { d.DeclarantName }, r.ConflictDetails);
        await _db.SaveChangesAsync(ct);
        return new DeclarationDto(d.Id, d.UserId, d.DeclarantName, d.HasConflict, d.ConflictDetails, d.ConfidentialityAccepted, d.DeclaredAtUtc);
    }

    // ----- Publication (FR-SCM-005) -----
    public async Task<PublicationDto> PublishAsync(Guid procurementId, SavePublicationRequest r, CancellationToken ct)
    {
        new Validator().Required("channel", r.Channel, 100).Required("reference", r.Reference, 100)
            .Must(DateOnly.FromDateTime(r.ClosingDateUtc) >= r.PublishedOn, "closingDateUtc", "Closing date must be after the publication date.").ThrowIfInvalid();
        var p = await LoadAsync(procurementId, ct);
        if (p.Status is not (ProcurementStatus.SpecificationApproved or ProcurementStatus.Published))
            throw new DomainException("Publish only after the specification/TOR is approved.", "FR-SCM-002");

        if (p.MethodRuleId is { } ruleId)
        {
            var rule = await _db.ProcurementMethodRules.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ruleId, ct);
            var days = DateOnly.FromDateTime(r.ClosingDateUtc).DayNumber - r.PublishedOn.DayNumber;
            if (rule is not null && rule.MinimumAdvertDays > 0 && days < rule.MinimumAdvertDays)
                throw new DomainException($"{rule.Name} requires at least {rule.MinimumAdvertDays} days between publication and closing (got {days}).", "FR-BUD-007");
        }

        var publication = new Publication
        {
            ProcurementId = procurementId, Channel = r.Channel, Reference = r.Reference, PublishedOn = r.PublishedOn,
            ClosingDateUtc = DateTime.SpecifyKind(r.ClosingDateUtc, DateTimeKind.Utc), Url = r.Url, BriefingSession = r.BriefingSession,
            EvidenceDocumentId = r.EvidenceDocumentId
        };
        _db.Publications.Add(publication);
        p.Status = ProcurementStatus.Published;
        p.ClosingDateUtc = publication.ClosingDateUtc;

        if (p.RequisitionId is { } rid)
        {
            var planItemId = await _db.Requisitions.Where(x => x.Id == rid).Select(x => x.PlanItemId).SingleAsync(ct);
            if (planItemId is { } pi)
            {
                var item = await _db.ProcurementPlanItems.SingleAsync(i => i.Id == pi, ct);
                item.ActualAdvertDate ??= r.PublishedOn;
            }
        }
        await _db.SaveChangesAsync(ct);
        return new PublicationDto(publication.Id, publication.Channel, publication.Reference, publication.PublishedOn, publication.ClosingDateUtc,
            publication.Url, publication.BriefingSession, publication.EvidenceDocumentId);
    }

    // ----- Bids (FR-SCM-006) -----
    public async Task<IReadOnlyList<BidDto>> ListBidsAsync(Guid procurementId, CancellationToken ct)
    {
        await LoadAsync(procurementId, ct, tracking: false);
        await EnsureBidAccessAsync(procurementId, ct);
        return await BidsAsync(procurementId, ct);
    }

    public async Task<BidDto> RegisterBidAsync(Guid procurementId, RegisterBidRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("supplierId", r.SupplierId).Required("bidReference", r.BidReference, 100).Positive("bidAmount", r.BidAmount)
            .NonNegative("specificGoalsClaimed", r.SpecificGoalsClaimed).ThrowIfInvalid();
        var p = await LoadAsync(procurementId, ct);
        if (p.Status is not (ProcurementStatus.Published or ProcurementStatus.BidsClosed))
            throw new DomainException("Bids can only be received for a published procurement.", "FR-SCM-006");
        var supplier = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == r.SupplierId, ct)
                       ?? throw new ValidationException("supplierId", "Supplier not found.");
        if (await _db.Bids.AnyAsync(b => b.ProcurementId == procurementId && b.SupplierId == r.SupplierId && b.Status != BidStatus.Withdrawn, ct))
            throw new ConflictException($"{supplier.LegalName} already has a bid on this procurement.");

        var now = _clock.UtcNow;   // secure server-side receipt timestamp
        var late = p.ClosingDateUtc is { } closing && now > closing;
        var bid = new Bid
        {
            ProcurementId = procurementId, SupplierId = r.SupplierId, BidReference = r.BidReference, BidAmount = r.BidAmount,
            SpecificGoalsClaimed = r.SpecificGoalsClaimed, SubmissionNotes = r.SubmissionNotes, ReceivedAtUtc = now, IsLate = late,
            Status = late ? BidStatus.Late : BidStatus.Received
        };
        _db.Bids.Add(bid);
        _audit.Write("Procurement", nameof(Bid), bid.Id.ToString(), late ? "LateBidReceived" : "BidReceived",
            new { p.Number, supplier.SupplierNumber, ReceivedAtUtc = now, p.ClosingDateUtc });
        await _db.SaveChangesAsync(ct);
        return (await BidsAsync(procurementId, ct)).Single(b => b.Id == bid.Id);
    }

    public async Task<IReadOnlyList<BidDto>> OpenBidsAsync(Guid procurementId, CancellationToken ct)
    {
        var p = await LoadAsync(procurementId, ct);
        if (p.Status != ProcurementStatus.Published) throw new DomainException("Bids can only be opened for a published procurement.", "FR-SCM-006");
        if (p.ClosingDateUtc is null || _clock.UtcNow < p.ClosingDateUtc)
            throw new DomainException($"Bids cannot be opened before the closing date ({p.ClosingDateUtc:yyyy-MM-dd HH:mm} UTC).", "FR-SCM-006");

        var bids = await _db.Bids.Where(b => b.ProcurementId == procurementId).ToListAsync(ct);
        foreach (var b in bids)
        {
            if (b.Status == BidStatus.Received)
            {
                b.Status = BidStatus.Opened;
                b.OpenedAtUtc = _clock.UtcNow;
                b.OpenedBy = _user.DisplayName ?? _user.Username;
            }
            else if (b.Status == BidStatus.Late)
            {
                b.InvalidReason ??= "Received after the closing date and time.";
            }
        }
        p.Status = ProcurementStatus.Evaluation;
        p.BidOpeningAtUtc = _clock.UtcNow;
        _audit.Write("Procurement", ProcurementEntityName, p.Id.ToString(), "BidsOpened",
            new { Opened = bids.Count(b => b.Status == BidStatus.Opened), Late = bids.Count(b => b.IsLate) });
        await _db.SaveChangesAsync(ct);
        return await BidsAsync(procurementId, ct);
    }

    public async Task<BidDto> InvalidateBidAsync(Guid procurementId, Guid bidId, string reason, CancellationToken ct)
    {
        new Validator().Required("reason", reason, 2000).ThrowIfInvalid();
        var p = await LoadAsync(procurementId, ct);
        p.EnsureNotTerminal();
        var bid = await _db.Bids.SingleOrDefaultAsync(b => b.Id == bidId && b.ProcurementId == procurementId, ct) ?? throw new NotFoundException("Bid", bidId);
        bid.Status = BidStatus.Invalid;
        bid.InvalidReason = reason;
        _audit.Write("Procurement", nameof(Bid), bid.Id.ToString(), "BidInvalidated", null, reason);
        await _db.SaveChangesAsync(ct);
        return (await BidsAsync(procurementId, ct)).Single(b => b.Id == bidId);
    }

    // ----- Evaluation (FR-SCM-007/008/009) -----
    public async Task<CriterionDto> SaveCriterionAsync(Guid procurementId, Guid? id, SaveCriterionRequest r, CancellationToken ct)
    {
        new Validator().Required("name", r.Name, 300).NonNegative("weight", r.Weight).Positive("maxScore", r.MaxScore).ThrowIfInvalid();
        var p = await LoadAsync(procurementId, ct);
        p.EnsureNotTerminal();
        if (await _db.EvaluationScores.AnyAsync(s => _db.Bids.Any(b => b.Id == s.BidId && b.ProcurementId == procurementId), ct))
            throw new DomainException("Criteria are locked once scoring has started.", "FR-SCM-008");
        EvaluationCriterion c;
        if (id is null)
        {
            c = new EvaluationCriterion { ProcurementId = procurementId };
            _db.EvaluationCriteria.Add(c);
        }
        else
        {
            c = await _db.EvaluationCriteria.SingleOrDefaultAsync(x => x.Id == id && x.ProcurementId == procurementId, ct) ?? throw new NotFoundException("Criterion", id);
        }
        c.Stage = r.Stage;
        c.Name = r.Name;
        c.Description = r.Description;
        c.Weight = r.Stage == EvaluationStage.Compliance ? 0 : r.Weight;
        c.MaxScore = r.Stage == EvaluationStage.Compliance ? 1 : r.MaxScore;
        c.IsMandatory = r.Stage == EvaluationStage.Compliance || r.IsMandatory;
        c.SortOrder = r.SortOrder;
        await _db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task DeleteCriterionAsync(Guid procurementId, Guid criterionId, CancellationToken ct)
    {
        await LoadAsync(procurementId, ct);
        if (await _db.EvaluationScores.AnyAsync(s => s.CriterionId == criterionId, ct))
            throw new DomainException("Criteria are locked once scoring has started.", "FR-SCM-008");
        var c = await _db.EvaluationCriteria.SingleOrDefaultAsync(x => x.Id == criterionId && x.ProcurementId == procurementId, ct)
                ?? throw new NotFoundException("Criterion", criterionId);
        _db.EvaluationCriteria.Remove(c);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetupEvaluationAsync(Guid procurementId, EvaluationSetupRequest r, CancellationToken ct)
    {
        new Validator().Range("technicalThreshold", r.TechnicalThreshold, 0, 100).ThrowIfInvalid();
        var p = await LoadAsync(procurementId, ct);
        p.EnsureNotTerminal();
        if (r.PriceSystemCode is not null)
        {
            // The price/preference system is fixed as at initiation of the process (SRS §28).
            var initiatedOn = DateOnly.FromDateTime(p.CreatedAtUtc);
            var system = (await _db.PricePreferenceSystems.AsNoTracking().Where(s => s.Code == r.PriceSystemCode).ToListAsync(ct))
                .Where(s => s.IsEffectiveOn(initiatedOn)).OrderByDescending(s => s.EffectiveFrom).FirstOrDefault()
                ?? throw new DomainException($"Price/preference system {r.PriceSystemCode} was not approved and effective on {initiatedOn:yyyy-MM-dd}.", "FR-SCM-009");
            if ((system.ApplicableFromValue is { } min && p.EstimatedValue < min) || (system.ApplicableToValue is { } max && p.EstimatedValue > max))
                throw new DomainException($"{system.Name} does not apply to an estimated value of R {p.EstimatedValue:N2}.", "FR-SCM-009");
            p.PriceSystemCode = system.Code;
            p.PriceSystemId = system.Id;
        }
        p.TechnicalThreshold = r.TechnicalThreshold;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ScoreDto>> MyScoresAsync(Guid procurementId, CancellationToken ct)
    {
        var userId = _user.UserId ?? throw new ForbiddenException();
        return await (from s in _db.EvaluationScores.AsNoTracking()
                      join b in _db.Bids.AsNoTracking() on s.BidId equals b.Id
                      where b.ProcurementId == procurementId && s.EvaluatorUserId == userId
                      select new ScoreDto(s.Id, s.BidId, s.CriterionId, s.EvaluatorUserId, s.EvaluatorName, s.Score, s.Passed, s.Comment, s.EvidenceReference, s.ScoredAtUtc))
            .ToListAsync(ct);
    }

    public async Task<ScoreDto> SubmitScoreAsync(Guid procurementId, SubmitScoreRequest r, CancellationToken ct)
    {
        new Validator().Required("comment", r.Comment, 4000).ThrowIfInvalid();
        var userId = _user.UserId ?? throw new ForbiddenException();
        var p = await LoadAsync(procurementId, ct, tracking: false);
        if (p.Status != ProcurementStatus.Evaluation) throw new DomainException("Scores can only be captured during evaluation.", "FR-SCM-008");
        await EnsureEvaluatorAsync(procurementId, userId, ct);

        var bid = await _db.Bids.AsNoTracking().SingleOrDefaultAsync(b => b.Id == r.BidId && b.ProcurementId == procurementId, ct)
                  ?? throw new NotFoundException("Bid", r.BidId);
        if (bid.Status is BidStatus.Late or BidStatus.Invalid or BidStatus.Withdrawn)
            throw new DomainException("Late, invalid or withdrawn bids are not evaluated.", "FR-SCM-006");
        var criterion = await _db.EvaluationCriteria.AsNoTracking().SingleOrDefaultAsync(c => c.Id == r.CriterionId && c.ProcurementId == procurementId, ct)
                        ?? throw new NotFoundException("Criterion", r.CriterionId);
        if (criterion.Stage == EvaluationStage.Compliance && r.Passed is null)
            throw new ValidationException("passed", "Record a pass/fail result for compliance criteria.");
        if (criterion.Stage == EvaluationStage.Technical && (r.Score is null || r.Score < 0 || r.Score > criterion.MaxScore))
            throw new ValidationException("score", $"Score must be between 0 and {criterion.MaxScore}.");

        var score = await _db.EvaluationScores.SingleOrDefaultAsync(s => s.BidId == r.BidId && s.CriterionId == r.CriterionId && s.EvaluatorUserId == userId, ct);
        if (score is null)
        {
            score = new EvaluationScore { BidId = r.BidId, CriterionId = r.CriterionId, EvaluatorUserId = userId };
            _db.EvaluationScores.Add(score);
        }
        score.EvaluatorName = _user.DisplayName ?? _user.Username ?? "?";
        score.Score = criterion.Stage == EvaluationStage.Technical ? r.Score : null;
        score.Passed = criterion.Stage == EvaluationStage.Compliance ? r.Passed : null;
        score.Comment = r.Comment;
        score.EvidenceReference = r.EvidenceReference;
        score.ScoredAtUtc = _clock.UtcNow;
        _ledger.Record(ProcurementEntityName, procurementId, "Evaluate", p.ProjectId);
        await _db.SaveChangesAsync(ct);
        return new ScoreDto(score.Id, score.BidId, score.CriterionId, score.EvaluatorUserId, score.EvaluatorName, score.Score, score.Passed, score.Comment,
            score.EvidenceReference, score.ScoredAtUtc);
    }

    public async Task<EvaluationReportDto> ConsolidateAsync(Guid procurementId, CancellationToken ct)
    {
        var p = await LoadAsync(procurementId, ct);
        if (p.Status != ProcurementStatus.Evaluation) throw new DomainException("Consolidation happens during the evaluation stage.", "FR-SCM-008");
        var criteria = await _db.EvaluationCriteria.AsNoTracking().Where(c => c.ProcurementId == procurementId).ToListAsync(ct);
        var bids = await _db.Bids.Where(b => b.ProcurementId == procurementId
            && b.Status != BidStatus.Late && b.Status != BidStatus.Invalid && b.Status != BidStatus.Withdrawn).ToListAsync(ct);
        if (bids.Count == 0) throw new DomainException("There are no valid bids to evaluate.", "FR-SCM-007");
        var bidIds = bids.Select(b => b.Id).ToList();
        var scores = await _db.EvaluationScores.AsNoTracking().Where(s => bidIds.Contains(s.BidId)).ToListAsync(ct);

        var compliance = criteria.Where(c => c.Stage == EvaluationStage.Compliance).ToList();
        var technical = criteria.Where(c => c.Stage == EvaluationStage.Technical)
            .Select(c => new TechnicalCriterion(c.Id, c.Name, c.Weight, c.MaxScore)).ToList();
        if (technical.Count > 0 && technical.Sum(t => t.Weight) != 100m)
            throw new DomainException("Technical weights must total 100.", "FR-SCM-008");

        foreach (var bid in bids)
        {
            // Compliance: every mandatory item passed by every evaluator who assessed it, with at least one assessment each.
            var complianceResults = compliance.SelectMany(c =>
            {
                var s = scores.Where(x => x.BidId == bid.Id && x.CriterionId == c.Id).ToList();
                return s.Count == 0 ? new[] { (c.IsMandatory, (bool?)null) } : s.Select(x => (c.IsMandatory, x.Passed)).ToArray();
            }).ToList();
            bid.ComplianceMet = EvaluationEngine.IsCompliant(complianceResults);
            if (bid.ComplianceMet != true)
            {
                bid.Status = BidStatus.NonCompliant;
                bid.TechnicalScore = null;
                continue;
            }
            bid.Status = BidStatus.Compliant;

            if (technical.Count > 0)
            {
                var perEvaluator = scores.Where(s => s.BidId == bid.Id && s.Score != null && technical.Any(t => t.Id == s.CriterionId))
                    .GroupBy(s => s.EvaluatorUserId)
                    .Select(g => (IReadOnlyDictionary<Guid, decimal>)g.ToDictionary(s => s.CriterionId, s => s.Score!.Value))
                    .ToList();
                bid.TechnicalScore = EvaluationEngine.ConsolidateTechnicalScore(technical, perEvaluator);
                bid.Status = p.TechnicalThreshold is { } threshold && bid.TechnicalScore < threshold ? BidStatus.NonResponsive : BidStatus.Responsive;
            }
            else
            {
                bid.Status = BidStatus.Responsive;
            }
        }

        var responsive = bids.Where(b => b.Status == BidStatus.Responsive).ToList();
        if (p.PriceSystemId is { } systemId && responsive.Count > 0)
        {
            var system = await _db.PricePreferenceSystems.AsNoTracking().SingleAsync(s => s.Id == systemId, ct);
            var results = EvaluationEngine.CalculatePricePreference(
                responsive.Select(b => new BidPriceInput(b.Id, b.BidAmount, b.SpecificGoalsClaimed ?? 0m, true)).ToList(),
                system.PricePoints, system.PreferencePoints);
            foreach (var result in results)
            {
                var bid = responsive.Single(b => b.Id == result.BidId);
                bid.PricePoints = result.PricePoints;
                bid.PreferencePoints = result.PreferencePoints;
                bid.TotalPoints = result.TotalPoints;
                bid.Rank = result.Rank;
                if (result.Rank == 1) bid.Status = BidStatus.Recommended;
            }
        }
        else if (responsive.Count > 0)
        {
            // No price/preference system: lowest acceptable price ranks first (e.g. RFQs below the threshold).
            var rank = 1;
            foreach (var bid in responsive.OrderBy(b => b.BidAmount))
            {
                bid.Rank = rank;
                if (rank == 1) bid.Status = BidStatus.Recommended;
                rank++;
            }
        }

        _audit.Write("Procurement", ProcurementEntityName, p.Id.ToString(), "EvaluationConsolidated",
            bids.Select(b => new { b.Id, b.Status, b.TechnicalScore, b.PricePoints, b.PreferencePoints, b.TotalPoints, b.Rank }));
        await _db.SaveChangesAsync(ct);
        return await EvaluationReportAsync(procurementId, ct);
    }

    public async Task<EvaluationReportDto> EvaluationReportAsync(Guid procurementId, CancellationToken ct)
    {
        var p = await LoadAsync(procurementId, ct, tracking: false);
        await EnsureBidAccessAsync(procurementId, ct);
        var criteria = await CriteriaAsync(procurementId, ct);
        var bids = await BidsAsync(procurementId, ct);
        var bidIds = bids.Select(b => b.Id).ToList();
        var scores = await _db.EvaluationScores.AsNoTracking().Where(s => bidIds.Contains(s.BidId)).OrderBy(s => s.EvaluatorName)
            .Select(s => new ScoreDto(s.Id, s.BidId, s.CriterionId, s.EvaluatorUserId, s.EvaluatorName, s.Score, s.Passed, s.Comment, s.EvidenceReference, s.ScoredAtUtc))
            .ToListAsync(ct);
        PricePreferenceSystem? system = p.PriceSystemId is { } sid ? await _db.PricePreferenceSystems.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sid, ct) : null;
        var notes = "Technical score = Σ(mean evaluator score ÷ max score × weight). " +
                    (system is null ? "No price/preference system: ranked by lowest acceptable price."
                        : $"Price points = {system.PricePoints} × (1 − (Pt − Pmin) ÷ Pmin); preference points capped at {system.PreferencePoints}; ranked on total points.");
        return new EvaluationReportDto(p.Id, p.Number, p.TechnicalThreshold, p.PriceSystemCode, system?.PricePoints, system?.PreferencePoints,
            criteria, scores, bids, p.Method, notes);
    }

    // ----- Due diligence (FR-SCM-010) -----
    public async Task<DueDiligenceDto> SaveDueDiligenceAsync(Guid procurementId, SaveDueDiligenceRequest r, CancellationToken ct)
    {
        var p = await LoadAsync(procurementId, ct);
        p.EnsureNotTerminal();
        var bid = await _db.Bids.AsNoTracking().SingleOrDefaultAsync(b => b.Id == r.BidId && b.ProcurementId == procurementId, ct)
                  ?? throw new NotFoundException("Bid", r.BidId);
        var check = await _db.DueDiligenceChecks.SingleOrDefaultAsync(d => d.BidId == r.BidId, ct);
        if (check is null)
        {
            check = new DueDiligenceCheck { ProcurementId = procurementId, BidId = r.BidId, SupplierId = bid.SupplierId };
            _db.DueDiligenceChecks.Add(check);
        }
        check.CsdVerified = r.CsdVerified;
        check.TaxCompliant = r.TaxCompliant;
        check.NotRestricted = r.NotRestricted;
        check.NotOnDefaultersList = r.NotOnDefaultersList;
        check.ReferencesChecked = r.ReferencesChecked;
        check.CapacityConfirmed = r.CapacityConfirmed;
        check.Notes = r.Notes;
        check.Outcome = check.AllChecksPassed ? DueDiligenceOutcome.Passed : DueDiligenceOutcome.Failed;
        check.CompletedAtUtc = _clock.UtcNow;
        check.CompletedBy = _user.DisplayName ?? _user.Username;
        await _db.SaveChangesAsync(ct);
        return ToDto(check);
    }

    public async Task<IReadOnlyList<DueDiligenceDto>> ListDueDiligenceAsync(Guid procurementId, CancellationToken ct)
    {
        await LoadAsync(procurementId, ct, tracking: false);
        return (await _db.DueDiligenceChecks.AsNoTracking().Where(d => d.ProcurementId == procurementId).ToListAsync(ct)).Select(ToDto).ToList();
    }

    // ----- Adjudication and award (FR-SCM-011/012, BR-004) -----
    public async Task<WorkflowInstanceDto> SubmitAdjudicationAsync(Guid procurementId, SubmitAdjudicationRequest r, CancellationToken ct)
    {
        new Validator().Required("recommendation", r.Recommendation, 8000).ThrowIfInvalid();
        var p = await LoadAsync(procurementId, ct);
        if (p.Status != ProcurementStatus.Evaluation) throw new DomainException("Adjudication follows a completed evaluation.", "FR-SCM-011");
        var bid = await _db.Bids.AsNoTracking().SingleOrDefaultAsync(b => b.Id == r.RecommendedBidId && b.ProcurementId == procurementId, ct)
                  ?? throw new NotFoundException("Bid", r.RecommendedBidId);
        if (bid.Status is not (BidStatus.Recommended or BidStatus.Responsive))
            throw new DomainException("Only a responsive bid can be recommended for award.", "FR-SCM-011");
        var dd = await _db.DueDiligenceChecks.AsNoTracking().SingleOrDefaultAsync(d => d.BidId == bid.Id, ct);
        if (dd is null || dd.Outcome != DueDiligenceOutcome.Passed)
            throw new DomainException("The recommendation must reference a passed due-diligence result for the recommended bidder.", "FR-SCM-010");

        var adjudication = new Adjudication
        {
            ProcurementId = procurementId, RecommendedBidId = bid.Id, DueDiligenceCheckId = dd.Id, Recommendation = r.Recommendation, Conditions = r.Conditions
        };
        _db.Adjudications.Add(adjudication);
        p.Status = ProcurementStatus.Adjudication;
        var instance = await _workflow.StartAsync(new StartWorkflowRequest(AdjudicationWorkflow, ProcurementEntityName, p.Id, p.Number,
            $"Adjudication: {p.Title}", bid.BidAmount, p.ProjectId, $"procurement/{p.Id}"), ct);
        adjudication.WorkflowInstanceId = instance.Id;
        await _db.SaveChangesAsync(ct);
        return await _workflow.GetAsync(instance.Id, ct);
    }

    public async Task<AwardDto> SatisfyAwardConditionsAsync(Guid awardId, string note, CancellationToken ct)
    {
        new Validator().Required("note", note, 2000).ThrowIfInvalid();
        var award = await _db.Awards.SingleOrDefaultAsync(a => a.Id == awardId, ct) ?? throw new NotFoundException("Award", awardId);
        await _scope.EnsureProjectAsync(award.ProjectId, ct);
        if (award.Status != AwardStatus.PendingConditions) throw new DomainException("The award has no outstanding conditions.", "BR-004");
        award.ConditionsSatisfied = true;
        award.Status = AwardStatus.Final;
        _audit.Write("Procurement", nameof(Award), award.Id.ToString(), "AwardConditionsSatisfied", null, note);
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(award, ct);
    }

    public async Task<IReadOnlyList<AwardDto>> ListAwardsAsync(bool pendingContractOnly, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.Awards.AsNoTracking().InScope(scope, a => a.ProjectId).Where(a => a.Status != AwardStatus.Cancelled);
        if (pendingContractOnly) q = q.Where(a => a.ContractId == null);
        var awards = await q.OrderByDescending(a => a.AwardDate).ToListAsync(ct);
        var list = new List<AwardDto>();
        foreach (var a in awards) list.Add(await ToDtoAsync(a, ct));
        return list;
    }

    // ----- Cancellation / re-advertisement (FR-SCM-014) -----
    public async Task<ProcurementDetailDto> CancelAsync(Guid procurementId, string reason, CancellationToken ct)
    {
        new Validator().Required("reason", reason, 4000).ThrowIfInvalid();
        var p = await LoadAsync(procurementId, ct);
        p.EnsureNotTerminal();
        await _workflow.CancelForEntityAsync(ProcurementEntityName, p.Id, reason, ct);
        p.Status = ProcurementStatus.Cancelled;
        p.CancellationReason = reason;
        p.CancelledAtUtc = _clock.UtcNow;
        _audit.Write("Procurement", ProcurementEntityName, p.Id.ToString(), "ProcurementCancelled", null, reason);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(procurementId, ct);
    }

    public async Task<ProcurementDetailDto> ReAdvertiseAsync(Guid procurementId, string reason, CancellationToken ct)
    {
        new Validator().Required("reason", reason, 4000).ThrowIfInvalid();
        var original = await LoadAsync(procurementId, ct);
        if (original.Status != ProcurementStatus.Cancelled) await CancelAsync(procurementId, reason, ct);
        original = await LoadAsync(procurementId, ct);
        if (original.ReAdvertisedAsId is not null) throw new ConflictException("This procurement has already been re-advertised.");

        var copy = new ProcurementEntity
        {
            Number = await _numbers.NextAsync(NumberPrefixes.Procurement, ct),
            ProjectId = original.ProjectId, RequisitionId = original.RequisitionId, Title = original.Title, Method = original.Method,
            MethodRuleId = original.MethodRuleId, MethodRuleVersion = original.MethodRuleVersion, EstimatedValue = original.EstimatedValue,
            TechnicalThreshold = original.TechnicalThreshold, PriceSystemCode = original.PriceSystemCode, PriceSystemId = original.PriceSystemId,
            ReAdvertisedFromId = original.Id, OrgUnit = original.OrgUnit, PlannedAwardDate = original.PlannedAwardDate
        };
        _db.Procurements.Add(copy);
        original.ReAdvertisedAsId = copy.Id;

        var spec = await _db.Specifications.AsNoTracking().Where(s => s.ProcurementId == original.Id).OrderByDescending(s => s.VersionNumber).FirstOrDefaultAsync(ct);
        _db.Specifications.Add(new Specification { ProcurementId = copy.Id, Title = spec?.Title ?? original.Title, Content = spec?.Content ?? string.Empty });
        foreach (var c in await _db.EvaluationCriteria.AsNoTracking().Where(c => c.ProcurementId == original.Id).ToListAsync(ct))
        {
            _db.EvaluationCriteria.Add(new EvaluationCriterion
            {
                ProcurementId = copy.Id, Stage = c.Stage, Name = c.Name, Description = c.Description, Weight = c.Weight, MaxScore = c.MaxScore,
                IsMandatory = c.IsMandatory, SortOrder = c.SortOrder
            });
        }
        _audit.Write("Procurement", ProcurementEntityName, original.Id.ToString(), "ReAdvertised", new { NewNumber = copy.Number }, reason);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(copy.Id, ct);
    }

    public async Task<IReadOnlyList<CommunicationDto>> ListCommunicationsAsync(Guid procurementId, CancellationToken ct)
    {
        await LoadAsync(procurementId, ct, tracking: false);
        return await (from c in _db.BidderCommunications.AsNoTracking()
                      join s in _db.Suppliers.AsNoTracking() on c.SupplierId equals (Guid?)s.Id into sj
                      from s in sj.DefaultIfEmpty()
                      where c.ProcurementId == procurementId
                      orderby c.Date descending
                      select new CommunicationDto(c.Id, c.BidId, c.SupplierId, s == null ? null : s.LegalName, c.Type.ToString(), c.Date, c.Subject,
                          c.Details, c.Outcome)).ToListAsync(ct);
    }

    public async Task<CommunicationDto> AddCommunicationAsync(Guid procurementId, SaveCommunicationRequest r, CancellationToken ct)
    {
        new Validator().Required("subject", r.Subject, 300).Required("details", r.Details, 8000).ThrowIfInvalid();
        await LoadAsync(procurementId, ct);
        var c = new BidderCommunication
        {
            ProcurementId = procurementId, BidId = r.BidId, SupplierId = r.SupplierId, Type = r.Type, Date = r.Date, Subject = r.Subject,
            Details = r.Details, Outcome = r.Outcome
        };
        _db.BidderCommunications.Add(c);
        await _db.SaveChangesAsync(ct);
        return (await ListCommunicationsAsync(procurementId, ct)).Single(x => x.Id == c.Id);
    }

    // =================== Exceptions (FR-SCM-015, BR-011) ===================
    public async Task<IReadOnlyList<ExceptionDto>> ListExceptionsAsync(string? status, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.ProcurementExceptions.AsNoTracking().InScopeNullable(scope, e => e.ProjectId);
        if (Enum.TryParse<ApprovalState>(status, true, out var st)) q = q.Where(e => e.Status == st);
        return (await q.OrderByDescending(e => e.CreatedAtUtc).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<ExceptionDto> SaveExceptionAsync(Guid? id, SaveExceptionRequest r, CancellationToken ct)
    {
        new Validator().Required("motivation", r.Motivation, 8000).Required("authority", r.Authority, 500).NonNegative("value", r.Value)
            .Must(r.ProjectId is not null || r.ProcurementId is not null || r.RequisitionId is not null, "projectId", "Link the exception to a project, requisition or procurement.")
            .ThrowIfInvalid();
        var projectId = r.ProjectId;
        if (r.RequisitionId is { } rid) projectId ??= await _db.Requisitions.Where(x => x.Id == rid).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct);
        if (r.ProcurementId is { } pid) projectId ??= await _db.Procurements.Where(x => x.Id == pid).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct);
        if (projectId is { } proj) await _scope.EnsureProjectAsync(proj, ct);

        ProcurementException e;
        if (id is null)
        {
            e = new ProcurementException { Number = await _numbers.NextAsync(NumberPrefixes.ProcurementException, ct) };
            _db.ProcurementExceptions.Add(e);
        }
        else
        {
            e = await _db.ProcurementExceptions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Exception", id);
            if (e.Status != ApprovalState.Draft) throw new DomainException("Only draft exceptions can be edited.", "BR-011");
        }
        e.ProjectId = projectId;
        e.ProcurementId = r.ProcurementId;
        e.RequisitionId = r.RequisitionId;
        e.Type = r.Type;
        e.Motivation = r.Motivation;
        e.Authority = r.Authority;
        e.Value = r.Value;
        await _db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    public async Task<WorkflowInstanceDto> SubmitExceptionAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.ProcurementExceptions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Exception", id);
        if (e.ProjectId is { } pid) await _scope.EnsureProjectAsync(pid, ct);
        if (e.Status != ApprovalState.Draft) throw new DomainException("Only draft exceptions can be submitted.", "BR-011");
        e.Status = ApprovalState.Pending;
        var instance = await _workflow.StartAsync(new StartWorkflowRequest(ExceptionWorkflow, nameof(ProcurementException), e.Id, e.Number,
            $"{e.Type} exception: {Truncate(e.Motivation, 80)}", e.Value, e.ProjectId, "procurement/exceptions"), ct);
        e.WorkflowInstanceId = instance.Id;
        await _db.SaveChangesAsync(ct);
        return await _workflow.GetAsync(instance.Id, ct);
    }

    // =================== Reporting ===================
    public async Task<IReadOnlyList<TransparencyRow>> TransparencyAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var procurements = await _db.Procurements.AsNoTracking().InScope(scope, p => p.ProjectId)
            .Where(p => p.Status == ProcurementStatus.Awarded || p.Status == ProcurementStatus.Cancelled).ToListAsync(ct);
        var ids = procurements.Select(p => p.Id).ToList();
        var bidsCount = await _db.Bids.AsNoTracking().Where(b => ids.Contains(b.ProcurementId)).GroupBy(b => b.ProcurementId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var awards = await _db.Awards.AsNoTracking().Where(a => ids.Contains(a.ProcurementId) && a.Status != AwardStatus.Cancelled).ToListAsync(ct);
        var supplierIds = awards.Select(a => a.SupplierId).ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var projectIds = procurements.Select(p => p.ProjectId).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        return procurements
            .Select(p =>
            {
                var award = awards.FirstOrDefault(a => a.ProcurementId == p.Id);
                var supplier = award is null ? null : suppliers.GetValueOrDefault(award.SupplierId);
                return new TransparencyRow(p.Number, p.Title, p.Method, p.ClosingDateUtc?.ToString("yyyy-MM-dd"), bidsCount.GetValueOrDefault(p.Id),
                    supplier?.LegalName, supplier?.RegistrationNumber, supplier?.BbbeeLevel, award?.Amount, award?.AwardDate.ToString("yyyy-MM-dd"),
                    p.Status.ToString(), projects.TryGetValue(p.ProjectId, out var pr) ? pr.Reference : "?", p.CancellationReason);
            })
            .Where(x => (from is null || x.AwardDate is null || string.CompareOrdinal(x.AwardDate, from.Value.ToString("yyyy-MM-dd")) >= 0)
                        && (to is null || x.AwardDate is null || string.CompareOrdinal(x.AwardDate, to.Value.ToString("yyyy-MM-dd")) <= 0))
            .OrderBy(x => x.TenderNumber)
            .ToList();
    }

    public async Task<ProcurementDashboardDto> DashboardAsync(Guid? projectId, string? orgUnit, string? method, string? status, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.Procurements.AsNoTracking().InScope(scope, p => p.ProjectId);
        if (projectId is { } pid) q = q.Where(p => p.ProjectId == pid);
        if (!string.IsNullOrWhiteSpace(orgUnit)) q = q.Where(p => p.OrgUnit == orgUnit);
        if (!string.IsNullOrWhiteSpace(method)) q = q.Where(p => p.Method == method);
        if (Enum.TryParse<ProcurementStatus>(status, true, out var st)) q = q.Where(p => p.Status == st);
        var rows = await q.ToListAsync(ct);
        var items = await ToListItemsAsync(rows, ct);
        var today = _clock.Today;
        var open = items.Where(i => i.Status is not (nameof(ProcurementStatus.Awarded) or nameof(ProcurementStatus.Cancelled))).ToList();
        var delayed = items.Where(i => i.PlannedAwardDate is { } planned && ((i.ActualAwardDate is { } actual && actual > planned) || (i.ActualAwardDate is null && planned < today && i.Status != nameof(ProcurementStatus.Cancelled)))).ToList();
        var ids = rows.Select(r => r.Id).ToList();
        var lateBids = await _db.Bids.CountAsync(b => ids.Contains(b.ProcurementId) && b.IsLate, ct);
        var exceptions = await _db.ProcurementExceptions.AsNoTracking().InScopeNullable(scope, e => e.ProjectId).Select(e => e.Status).ToListAsync(ct);

        return new ProcurementDashboardDto(items.Count, items.Sum(i => i.EstimatedValue),
            items.GroupBy(i => i.Status).Select(g => new StatusBucket(g.Key, g.Count(), g.Sum(x => x.EstimatedValue))).OrderByDescending(b => b.Count).ToList(),
            new[]
            {
                new AgeBucket("0-30 days", open.Count(i => i.DaysOpen <= 30)),
                new AgeBucket("31-60 days", open.Count(i => i.DaysOpen is > 30 and <= 60)),
                new AgeBucket("61-90 days", open.Count(i => i.DaysOpen is > 60 and <= 90)),
                new AgeBucket("90+ days", open.Count(i => i.DaysOpen > 90))
            },
            items.GroupBy(i => i.Method).Select(g => new MethodBucket(g.Key, g.Count(), g.Sum(x => x.EstimatedValue))).OrderByDescending(b => b.Value).ToList(),
            delayed.Count, exceptions.Count(s => s == ApprovalState.Approved), exceptions.Count(s => s == ApprovalState.Pending), lateBids,
            open.OrderByDescending(i => i.DaysOpen).Take(10).ToList(), delayed.Take(20).ToList());
    }

    // =================== helpers ===================
    private async Task<ProcurementEntity> LoadAsync(Guid id, CancellationToken ct, bool tracking = true)
    {
        var q = tracking ? _db.Procurements : _db.Procurements.AsNoTracking();
        var p = await q.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Procurement", id);
        // Committee members/evaluators reach assigned procurements even without general project scope.
        var scope = await _scope.GetAsync(ct);
        if (!scope.Includes(p.ProjectId) && !(_user.UserId is { } uid && await IsCommitteeMemberAsync(id, uid, ct)))
            throw new NotFoundException("Procurement", id);
        return p;
    }

    private async Task<bool> IsCommitteeMemberAsync(Guid procurementId, Guid userId, CancellationToken ct)
    {
        var today = _clock.Today;
        return await (from m in _db.CommitteeMembers
                      join c in _db.Committees on m.CommitteeId equals c.Id
                      where c.ProcurementId == procurementId && m.UserId == userId && m.AccessFrom <= today && m.AccessTo >= today
                      select m).AnyAsync(ct);
    }

    private async Task<(bool CanAccess, bool IsEvaluator, bool Declared)> AccessAsync(Guid procurementId, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return (false, false, false);
        var declaration = await _db.Declarations.AsNoTracking().SingleOrDefaultAsync(d => d.ProcurementId == procurementId && d.UserId == userId, ct);
        var member = await IsCommitteeMemberAsync(procurementId, userId, ct);
        var declaredOk = declaration?.GrantsAccess == true;
        var admin = _user.HasPermission(Permissions.ProcurementManage);
        // BR-003: bid content only after a completed declaration without conflict (SCM administrators included).
        return ((member || admin) && declaredOk, member && _user.HasPermission(Permissions.ProcurementEvaluate), declaration is not null);
    }

    private async Task EnsureBidAccessAsync(Guid procurementId, CancellationToken ct)
    {
        var (canAccess, _, declared) = await AccessAsync(procurementId, ct);
        if (!canAccess)
            throw new ForbiddenException(declared
                ? "Your declaration records a conflict of interest; bid content is not available to you."
                : "Complete the conflict-of-interest and confidentiality declaration before accessing bid content.", "BR-003");
    }

    private async Task EnsureEvaluatorAsync(Guid procurementId, Guid userId, CancellationToken ct)
    {
        if (!_user.HasPermission(Permissions.ProcurementEvaluate)) throw new ForbiddenException("You are not an evaluator.");
        var today = _clock.Today;
        var isEvaluationMember = await (from m in _db.CommitteeMembers
                                        join c in _db.Committees on m.CommitteeId equals c.Id
                                        where c.ProcurementId == procurementId && c.Type == CommitteeType.BidEvaluation && m.UserId == userId
                                              && m.AccessFrom <= today && m.AccessTo >= today
                                        select m).AnyAsync(ct);
        if (!isEvaluationMember) throw new ForbiddenException("You are not a current member of this procurement's evaluation committee.", "FR-SCM-003");
        await EnsureBidAccessAsync(procurementId, ct);
    }

    private async Task<List<CommitteeDto>> CommitteesAsync(Guid? procurementId, CancellationToken ct)
    {
        var today = _clock.Today;
        var committees = await _db.Committees.AsNoTracking().Include(c => c.Members).Where(c => c.ProcurementId == procurementId).ToListAsync(ct);
        var declarations = procurementId is null ? new List<Declaration>()
            : await _db.Declarations.AsNoTracking().Where(d => d.ProcurementId == procurementId).ToListAsync(ct);
        return committees.OrderBy(c => c.Type).Select(c => new CommitteeDto(c.Id, c.Type.ToString(), c.Name, c.Quorum,
            c.Members.OrderBy(m => m.Role).ThenBy(m => m.Name).Select(m =>
            {
                var d = declarations.FirstOrDefault(x => x.UserId == m.UserId);
                return new CommitteeMemberDto(m.Id, m.UserId, m.Name, m.Role.ToString(), m.AccessFrom, m.AccessTo, m.HasAccessOn(today), d is not null, d?.HasConflict);
            }).ToList())).ToList();
    }

    private async Task<List<CriterionDto>> CriteriaAsync(Guid procurementId, CancellationToken ct) =>
        (await _db.EvaluationCriteria.AsNoTracking().Where(c => c.ProcurementId == procurementId).OrderBy(c => c.Stage).ThenBy(c => c.SortOrder).ToListAsync(ct))
        .Select(ToDto).ToList();

    private async Task<List<BidDto>> BidsAsync(Guid procurementId, CancellationToken ct)
    {
        var rows = await (from b in _db.Bids.AsNoTracking()
                          join s in _db.Suppliers.AsNoTracking() on b.SupplierId equals s.Id
                          where b.ProcurementId == procurementId
                          select new { b, s }).ToListAsync(ct);
        var procurement = await _db.Procurements.AsNoTracking().SingleAsync(p => p.Id == procurementId, ct);
        // Bid prices remain sealed until the bids are formally opened.
        var sealedPrices = procurement.Status is ProcurementStatus.Published;
        return rows.OrderBy(x => x.b.Rank ?? int.MaxValue).ThenBy(x => x.b.ReceivedAtUtc).Select(x => new BidDto(x.b.Id, x.s.Id, x.s.SupplierNumber, x.s.LegalName,
            x.s.BbbeeLevel, x.b.BidReference, x.b.ReceivedAtUtc, x.b.IsLate, sealedPrices ? null : x.b.BidAmount, x.b.Status.ToString(), x.b.OpenedAtUtc,
            x.b.OpenedBy, x.b.InvalidReason, x.b.ComplianceMet, x.b.TechnicalScore, x.b.PricePoints, x.b.PreferencePoints, x.b.SpecificGoalsClaimed,
            x.b.TotalPoints, x.b.Rank)).ToList();
    }

    private async Task<List<ProcurementListItemDto>> ToListItemsAsync(IReadOnlyCollection<ProcurementEntity> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = p.ProjectNumber ?? p.DraftReference }).ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        var bidCounts = await _db.Bids.AsNoTracking().Where(b => ids.Contains(b.ProcurementId)).GroupBy(b => b.ProcurementId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var now = _clock.UtcNow;
        return rows.Select(p => new ProcurementListItemDto(p.Id, p.Number, p.Title, p.ProjectId, projects.GetValueOrDefault(p.ProjectId, "?"), p.Method,
            p.EstimatedValue, p.Status.ToString(), p.ClosingDateUtc, bidCounts.GetValueOrDefault(p.Id),
            (int)((p.CancelledAtUtc ?? (p.ActualAwardDate?.ToDateTime(TimeOnly.MinValue)) ?? now) - p.CreatedAtUtc).TotalDays,
            p.OrgUnit, p.PlannedAwardDate, p.ActualAwardDate)).ToList();
    }

    private async Task<List<RequisitionDto>> ToDtosAsync(IReadOnlyCollection<Requisition> rows, CancellationToken ct)
    {
        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = p.ProjectNumber ?? p.DraftReference }).ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        return rows.Select(r => new RequisitionDto(r.Id, r.Number, r.ProjectId, projects.GetValueOrDefault(r.ProjectId, "?"), r.PlanItemId, r.BudgetLineId,
            r.Title, r.Description, r.EstimatedValue, r.RequiredByDate, r.RecommendedMethod, r.SelectedMethod, r.MethodJustification, r.BudgetAvailable,
            r.BudgetCheckResult, r.ExceptionId, r.Status.ToString(), r.ProcurementId, r.CreatedBy, r.CreatedAtUtc, r.Version)).ToList();
    }

    private async Task<AdjudicationDto> ToDtoAsync(Adjudication a, CancellationToken ct)
    {
        var bid = await _db.Bids.AsNoTracking().SingleAsync(b => b.Id == a.RecommendedBidId, ct);
        var supplier = await _db.Suppliers.AsNoTracking().SingleAsync(s => s.Id == bid.SupplierId, ct);
        return new AdjudicationDto(a.Id, a.RecommendedBidId, supplier.LegalName, bid.BidAmount, a.DueDiligenceCheckId, a.Recommendation,
            a.Decision.ToString(), a.Conditions, a.Reasons, a.DecidedAtUtc, a.DecidedBy, a.WorkflowInstanceId);
    }

    private async Task<AwardDto> ToDtoAsync(Award a, CancellationToken ct)
    {
        var supplier = await _db.Suppliers.AsNoTracking().SingleAsync(s => s.Id == a.SupplierId, ct);
        var procurement = await _db.Procurements.AsNoTracking().SingleAsync(p => p.Id == a.ProcurementId, ct);
        return new AwardDto(a.Id, a.Number, a.ProcurementId, procurement.Number, a.BidId, a.SupplierId, supplier.LegalName, a.ProjectId, a.Amount,
            a.AwardDate, a.Status.ToString(), a.Conditions, a.ConditionsSatisfied, a.ContractId);
    }

    private static SpecificationDto ToDto(Specification s) =>
        new(s.Id, s.VersionNumber, s.Title, s.Content, s.Status.ToString(), s.ApprovedAtUtc, s.ApprovedBy, s.CreatedBy, s.Version);

    private static CriterionDto ToDto(EvaluationCriterion c) =>
        new(c.Id, c.Stage.ToString(), c.Name, c.Description, c.Weight, c.MaxScore, c.IsMandatory, c.SortOrder);

    private static DueDiligenceDto ToDto(DueDiligenceCheck d) =>
        new(d.Id, d.BidId, d.SupplierId, d.CsdVerified, d.TaxCompliant, d.NotRestricted, d.NotOnDefaultersList, d.ReferencesChecked, d.CapacityConfirmed,
            d.Outcome.ToString(), d.Notes, d.CompletedAtUtc, d.CompletedBy);

    private static ExceptionDto ToDto(ProcurementException e) =>
        new(e.Id, e.Number, e.ProjectId, e.ProcurementId, e.RequisitionId, e.Type.ToString(), e.Motivation, e.Authority, e.Value, e.Status.ToString(),
            e.DecidedAtUtc, e.DecidedBy, e.DecisionReason, e.CreatedBy, e.CreatedAtUtc);

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}

/// <summary>Requisition approval completes (FR-SCM-001).</summary>
public sealed class RequisitionApprovalHandler : IWorkflowCompletionHandler
{
    private readonly ITetaDbContext _db;
    public RequisitionApprovalHandler(ITetaDbContext db) => _db = db;
    public string EntityType => nameof(Requisition);

    public async Task OnCompletedAsync(WorkflowInstance instance, WorkflowState outcome, string? comment, CancellationToken ct)
    {
        var req = await _db.Requisitions.SingleAsync(r => r.Id == instance.EntityId, ct);
        req.Status = outcome switch
        {
            WorkflowState.Approved => RequisitionStatus.Approved,
            WorkflowState.Rejected => RequisitionStatus.Rejected,
            _ => RequisitionStatus.Draft
        };
    }
}

/// <summary>Deviation/exception approval completes (FR-SCM-015).</summary>
public sealed class ProcurementExceptionApprovalHandler : IWorkflowCompletionHandler
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;
    public ProcurementExceptionApprovalHandler(ITetaDbContext db, IClock clock) { _db = db; _clock = clock; }
    public string EntityType => nameof(ProcurementException);

    public async Task OnCompletedAsync(WorkflowInstance instance, WorkflowState outcome, string? comment, CancellationToken ct)
    {
        var e = await _db.ProcurementExceptions.SingleAsync(x => x.Id == instance.EntityId, ct);
        e.Status = outcome switch
        {
            WorkflowState.Approved => ApprovalState.Approved,
            WorkflowState.Rejected => ApprovalState.Rejected,
            _ => ApprovalState.Draft
        };
        e.DecidedAtUtc = _clock.UtcNow;
        e.DecidedBy = instance.Tasks.Where(t => t.DecidedAtUtc != null).OrderByDescending(t => t.DecidedAtUtc).FirstOrDefault()?.DecidedBy;
        e.DecisionReason = comment;
    }
}

/// <summary>
/// Adjudication approval completes → award record, bidder outcomes and downstream contract trigger
/// (FR-SCM-011/012/013, BR-004).
/// </summary>
public sealed class AdjudicationApprovalHandler : IWorkflowCompletionHandler
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;
    private readonly INumberGenerator _numbers;
    private readonly INotifier _notifier;

    public AdjudicationApprovalHandler(ITetaDbContext db, IClock clock, INumberGenerator numbers, INotifier notifier)
    {
        _db = db;
        _clock = clock;
        _numbers = numbers;
        _notifier = notifier;
    }

    public string EntityType => ProcurementService.ProcurementEntityName;

    public async Task OnCompletedAsync(WorkflowInstance instance, WorkflowState outcome, string? comment, CancellationToken ct)
    {
        var procurement = await _db.Procurements.SingleAsync(p => p.Id == instance.EntityId, ct);
        var adjudication = await _db.Adjudications.Where(a => a.ProcurementId == procurement.Id && a.Decision == AdjudicationDecision.Pending)
            .OrderByDescending(a => a.CreatedAtUtc).FirstAsync(ct);
        adjudication.DecidedAtUtc = _clock.UtcNow;
        adjudication.DecidedBy = instance.Tasks.Where(t => t.DecidedAtUtc != null).OrderByDescending(t => t.DecidedAtUtc).FirstOrDefault()?.DecidedBy;
        adjudication.Reasons = comment;

        if (outcome != WorkflowState.Approved)
        {
            adjudication.Decision = outcome == WorkflowState.Rejected ? AdjudicationDecision.Rejected : AdjudicationDecision.ReferredBack;
            procurement.Status = ProcurementStatus.Evaluation;
            return;
        }

        adjudication.Decision = AdjudicationDecision.Approved;
        var bids = await _db.Bids.Where(b => b.ProcurementId == procurement.Id).ToListAsync(ct);
        var winner = bids.Single(b => b.Id == adjudication.RecommendedBidId);
        winner.Status = BidStatus.Awarded;
        foreach (var other in bids.Where(b => b.Id != winner.Id && b.Status is BidStatus.Responsive or BidStatus.Recommended
                     or BidStatus.Compliant or BidStatus.NonResponsive or BidStatus.NonCompliant or BidStatus.Opened))
        {
            other.Status = BidStatus.Unsuccessful;
            _db.BidderCommunications.Add(new BidderCommunication
            {
                ProcurementId = procurement.Id, BidId = other.Id, SupplierId = other.SupplierId, Type = CommunicationType.RegretLetter,
                Date = _clock.Today, Subject = $"Outcome of {procurement.Number}",
                Details = $"Your bid for {procurement.Title} was unsuccessful. You may request a debriefing within the period stated in the bid documents."
            });
        }

        var hasConditions = !string.IsNullOrWhiteSpace(adjudication.Conditions);
        var award = new Award
        {
            Number = await _numbers.NextAsync(NumberPrefixes.Award, ct), ProcurementId = procurement.Id, BidId = winner.Id, SupplierId = winner.SupplierId,
            ProjectId = procurement.ProjectId, Amount = winner.BidAmount, AwardDate = _clock.Today,
            Status = hasConditions ? AwardStatus.PendingConditions : AwardStatus.Final, Conditions = adjudication.Conditions,
            ConditionsSatisfied = !hasConditions
        };
        _db.Awards.Add(award);
        _db.BidderCommunications.Add(new BidderCommunication
        {
            ProcurementId = procurement.Id, BidId = winner.Id, SupplierId = winner.SupplierId, Type = CommunicationType.AwardNotice, Date = _clock.Today,
            Subject = $"Award of {procurement.Number}", Details = $"Award {award.Number} for R {award.Amount:N2}."
        });
        procurement.Status = ProcurementStatus.Awarded;
        procurement.ActualAwardDate = _clock.Today;

        if (procurement.RequisitionId is { } rid)
        {
            var planItemId = await _db.Requisitions.Where(r => r.Id == rid).Select(r => r.PlanItemId).SingleAsync(ct);
            if (planItemId is { } pi)
            {
                var item = await _db.ProcurementPlanItems.SingleAsync(i => i.Id == pi, ct);
                item.Status = PlanItemStatus.Awarded;
                item.ActualAwardDate = _clock.Today;
                item.ActualValue = award.Amount;
            }
        }

        var supplier = await _db.Suppliers.AsNoTracking().SingleAsync(s => s.Id == winner.SupplierId, ct);
        await _notifier.NotifyRoleAsync(Roles.ContractManager, NotificationTemplates.AwardNotification,
            new Dictionary<string, string?> { ["Reference"] = procurement.Number, ["Supplier"] = supplier.LegalName, ["Amount"] = $"R {award.Amount:N2}" },
            "contracts", nameof(Award), award.Id, procurement.ProjectId, ct);
    }
}
