using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Documents;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Scm;
using Teta.Ippcms.Domain.Workflow;
using ContractEntity = Teta.Ippcms.Domain.Contracts.Contract;

namespace Teta.Ippcms.Application.ContractManagement;

// ---------- Contracts (DTOs) ----------
public sealed record ContractListItemDto(Guid Id, string ContractNumber, string Title, Guid ProjectId, string ProjectReference, Guid SupplierId,
    string SupplierName, decimal OriginalValue, decimal ApprovedVariations, decimal RevisedValue, decimal Committed, decimal Invoiced, decimal Paid,
    DateOnly StartDate, DateOnly OriginalEndDate, DateOnly CurrentEndDate, int DaysToExpiry, string Status, string SignatureStatus,
    string? ContractManagerName, decimal? PerformanceRating, int OpenBreaches, string Source);

public sealed record ContractQuery(string? Search, Guid? ProjectId, Guid? SupplierId, string? Status, bool? ExpiringOnly, bool IncludeHistorical = true,
    int Page = 1, int PageSize = 50);

public sealed record CreateContractFromAwardRequest(Guid AwardId, string Title, DateOnly StartDate, DateOnly EndDate, Guid? ContractManagerUserId,
    string? ContractManagerName, string? PoReference);
public sealed record CreateNonBidContractRequest(Guid ProjectId, Guid SupplierId, Guid ExceptionId, string Title, decimal Value, DateOnly StartDate,
    DateOnly EndDate, Guid? ContractManagerUserId, string? ContractManagerName, string? PoReference);
public sealed record UpdateContractRequest(string Title, Guid? ContractManagerUserId, string? ContractManagerName, string? PoReference);
public sealed record SignContractRequest(DateOnly SignedDate, Guid SignedDocumentId);

public sealed record ObligationDto(Guid Id, Guid ContractId, string Type, string Description, Guid? OwnerUserId, string OwnerName, DateOnly DueDate,
    string? EvidenceRequirement, string? KpiTarget, string Status, bool IsOverdue, long Version);
public sealed record SaveObligationRequest(ObligationType Type, string Description, Guid? OwnerUserId, string OwnerName, DateOnly DueDate,
    string? EvidenceRequirement, string? KpiTarget, ObligationStatus? Status);

public sealed record DeliverableDto(Guid Id, string Number, Guid ProjectId, Guid? ContractId, string? ContractNumber, Guid? MilestoneId, string Name,
    string? Description, DateOnly DueDate, decimal PayableAmount, decimal Invoiced, string? AcceptanceCriteria, bool EvidenceRequired,
    string AcceptanceStatus, DateTime? SubmittedAtUtc, DateTime? AcceptedAtUtc, string? AcceptedBy, string? RejectionReason, int EvidenceCount,
    bool IsOverdue, long Version);
public sealed record SaveDeliverableRequest(Guid ProjectId, Guid? ContractId, Guid? MilestoneId, string Name, string? Description, DateOnly DueDate,
    decimal PayableAmount, string? AcceptanceCriteria, bool EvidenceRequired);
public sealed record DeliverableDecisionRequest(bool Accept, string? Reason);

public sealed record PaymentScheduleDto(Guid Id, Guid ContractId, Guid? DeliverableId, string Description, decimal Amount, DateOnly PlannedDate);
public sealed record SavePaymentScheduleRequest(Guid? DeliverableId, string Description, decimal Amount, DateOnly PlannedDate);

public sealed record VariationDto(Guid Id, string Number, Guid ContractId, string Type, bool IsExtension, string Description, string Reason,
    decimal Amount, int Days, DateOnly? RevisedEndDate, decimal ValueBefore, decimal? ValueAfter, DateOnly EndDateBefore, DateOnly? EndDateAfter,
    string Status, DateTime? DecidedAtUtc, string? DecidedBy, Guid? WorkflowInstanceId, string? CreatedBy, DateTime CreatedAtUtc);
public sealed record SaveVariationRequest(VariationType Type, bool IsExtension, string Description, string Reason, decimal Amount, int Days,
    DateOnly? RevisedEndDate, Guid? ChangeRequestId);

public sealed record ReviewDto(Guid Id, Guid ContractId, string Period, DateOnly ReviewDate, decimal QualityScore, decimal TimelinessScore,
    decimal ComplianceScore, decimal OverallScore, string Rating, string? Comments, string? ReviewedBy);
public sealed record SaveReviewRequest(string Period, DateOnly ReviewDate, decimal QualityScore, decimal TimelinessScore, decimal ComplianceScore, string? Comments);

public sealed record BreachDto(Guid Id, Guid ContractId, string Description, string Severity, DateOnly IdentifiedOn, DateOnly? NoticeDate,
    string? NoticeReference, string? Remedy, DateOnly? RemedyDueDate, decimal? PenaltyAmount, string Status, bool IsOpen, long Version);
public sealed record SaveBreachRequest(string Description, Severity Severity, DateOnly IdentifiedOn, DateOnly? NoticeDate, string? NoticeReference,
    string? Remedy, DateOnly? RemedyDueDate, decimal? PenaltyAmount, BreachStatus? Status);

public sealed record CloseOutCheckDto(string Item, bool Met, string Detail);
public sealed record CloseOutResultDto(bool CanClose, IReadOnlyList<CloseOutCheckDto> Checks);
public sealed record CloseContractRequest(string ClosureNotes, string? ApprovedExceptionReference);

public sealed record ContractDetailDto(ContractListItemDto Summary, Guid? ProcurementId, string? ProcurementNumber, Guid? AwardId, string? AwardNumber,
    string? NonBidAuthority, Guid? ContractManagerUserId, string? PoReference, DateOnly? SignedDate, string? ClosureNotes,
    IReadOnlyList<ObligationDto> Obligations, IReadOnlyList<DeliverableDto> Deliverables, IReadOnlyList<PaymentScheduleDto> PaymentSchedule,
    IReadOnlyList<VariationDto> Variations, IReadOnlyList<ReviewDto> Reviews, IReadOnlyList<BreachDto> Breaches, long Version);

public sealed record ContractDashboardDto(int Active, decimal ActiveValue, decimal OriginalValue, decimal VariationValue, int ExpiringIn90Days,
    int Expired, int PendingVariations, int OpenBreaches, decimal? AveragePerformance, IReadOnlyList<ContractListItemDto> Expiring,
    IReadOnlyList<ContractListItemDto> Exceptions, IReadOnlyList<NameValue> ByStatus, IReadOnlyList<NameValue> RatingDistribution);
public sealed record NameValue(string Name, decimal Value);

public interface IContractService
{
    Task<PagedResult<ContractListItemDto>> ListAsync(ContractQuery query, CancellationToken ct);
    Task<ContractDetailDto> GetAsync(Guid id, CancellationToken ct);
    Task<ContractDetailDto> CreateFromAwardAsync(CreateContractFromAwardRequest request, CancellationToken ct);
    Task<ContractDetailDto> CreateNonBidAsync(CreateNonBidContractRequest request, CancellationToken ct);
    Task<ContractDetailDto> UpdateAsync(Guid id, UpdateContractRequest request, CancellationToken ct);
    Task<ContractDetailDto> SignAsync(Guid id, SignContractRequest request, CancellationToken ct);

    Task<ObligationDto> SaveObligationAsync(Guid contractId, Guid? id, SaveObligationRequest request, CancellationToken ct);
    Task<IReadOnlyList<DeliverableDto>> ListDeliverablesAsync(Guid? projectId, Guid? contractId, CancellationToken ct);
    Task<DeliverableDto> SaveDeliverableAsync(Guid? id, SaveDeliverableRequest request, CancellationToken ct);
    Task<DeliverableDto> SubmitDeliverableAsync(Guid id, CancellationToken ct);
    Task<DeliverableDto> DecideDeliverableAsync(Guid id, DeliverableDecisionRequest request, CancellationToken ct);
    Task<PaymentScheduleDto> SavePaymentScheduleAsync(Guid contractId, Guid? id, SavePaymentScheduleRequest request, CancellationToken ct);

    Task<VariationDto> SaveVariationAsync(Guid contractId, Guid? id, SaveVariationRequest request, CancellationToken ct);
    Task<WorkflowInstanceDto> SubmitVariationAsync(Guid contractId, Guid variationId, CancellationToken ct);
    Task<ReviewDto> AddReviewAsync(Guid contractId, SaveReviewRequest request, CancellationToken ct);
    Task<BreachDto> SaveBreachAsync(Guid contractId, Guid? id, SaveBreachRequest request, CancellationToken ct);

    Task<CloseOutResultDto> CloseOutChecksAsync(Guid id, CancellationToken ct);
    Task<ContractDetailDto> CloseAsync(Guid id, CloseContractRequest request, CancellationToken ct);
    Task<ContractDashboardDto> DashboardAsync(Guid? projectId, CancellationToken ct);
    Task<decimal> CommittedAsync(Guid contractId, CancellationToken ct);
}

/// <summary>Contract management (SRS §5.5), BR-004/005/006/010.</summary>
public sealed class ContractService : IContractService
{
    public const string VariationWorkflow = "CONTRACT_VARIATION";

    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAccessScope _scope;
    private readonly INumberGenerator _numbers;
    private readonly IWorkflowService _workflow;
    private readonly ITransactionLedger _ledger;
    private readonly ISodService _sod;
    private readonly IAuditWriter _audit;

    public ContractService(ITetaDbContext db, ICurrentUser user, IClock clock, IAccessScope scope, INumberGenerator numbers, IWorkflowService workflow,
        ITransactionLedger ledger, ISodService sod, IAuditWriter audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _scope = scope;
        _numbers = numbers;
        _workflow = workflow;
        _ledger = ledger;
        _sod = sod;
        _audit = audit;
    }

    public async Task<PagedResult<ContractListItemDto>> ListAsync(ContractQuery q, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var query = _db.Contracts.AsNoTracking().InScope(scope, c => c.ProjectId);
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(c => c.Title.Contains(q.Search) || c.ContractNumber.Contains(q.Search));
        if (q.ProjectId is { } pid) query = query.Where(c => c.ProjectId == pid);
        if (q.SupplierId is { } sid) query = query.Where(c => c.SupplierId == sid);
        if (Enum.TryParse<ContractStatus>(q.Status, true, out var st)) query = query.Where(c => c.Status == st);
        if (!q.IncludeHistorical) query = query.Where(c => c.Status != ContractStatus.Closed && c.Status != ContractStatus.Terminated);
        if (q.ExpiringOnly == true)
        {
            var horizon = _clock.Today.AddDays(90);
            query = query.Where(c => c.Status == ContractStatus.Active && c.CurrentEndDate <= horizon);
        }
        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 1000);
        var rows = await query.OrderBy(c => c.CurrentEndDate).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<ContractListItemDto>(await ToListItemsAsync(rows, ct), total, page, size);
    }

    public async Task<ContractDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var c = await LoadAsync(id, ct, tracking: false);
        var summary = (await ToListItemsAsync(new[] { c }, ct))[0];
        var procurement = c.ProcurementId is { } pid ? await _db.Procurements.AsNoTracking().SingleOrDefaultAsync(p => p.Id == pid, ct) : null;
        var award = c.AwardId is { } aid ? await _db.Awards.AsNoTracking().SingleOrDefaultAsync(a => a.Id == aid, ct) : null;
        var today = _clock.Today;
        var obligations = (await _db.ContractObligations.AsNoTracking().Where(o => o.ContractId == id).OrderBy(o => o.DueDate).ToListAsync(ct))
            .Select(o => ToDto(o, today)).ToList();
        var deliverables = await ListDeliverablesAsync(null, id, ct);
        var schedule = await _db.PaymentScheduleItems.AsNoTracking().Where(p => p.ContractId == id).OrderBy(p => p.PlannedDate)
            .Select(p => new PaymentScheduleDto(p.Id, p.ContractId, p.DeliverableId, p.Description, p.Amount, p.PlannedDate)).ToListAsync(ct);
        var variations = (await _db.ContractVariations.AsNoTracking().Where(v => v.ContractId == id).OrderBy(v => v.CreatedAtUtc).ToListAsync(ct))
            .Select(ToDto).ToList();
        var reviews = (await _db.ContractPerformanceReviews.AsNoTracking().Where(r => r.ContractId == id).OrderByDescending(r => r.ReviewDate).ToListAsync(ct))
            .Select(ToDto).ToList();
        var breaches = (await _db.ContractBreaches.AsNoTracking().Where(b => b.ContractId == id).OrderByDescending(b => b.IdentifiedOn).ToListAsync(ct))
            .Select(ToDto).ToList();
        return new ContractDetailDto(summary, c.ProcurementId, procurement?.Number, c.AwardId, award?.Number, c.NonBidAuthority, c.ContractManagerUserId,
            c.PoReference, c.SignedDate, c.ClosureNotes, obligations, deliverables, schedule, variations, reviews, breaches, c.Version);
    }

    /// <summary>FR-CON-001 / BR-004: award data is inherited without re-keying; only final awards with conditions met.</summary>
    public async Task<ContractDetailDto> CreateFromAwardAsync(CreateContractFromAwardRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("awardId", r.AwardId).Required("title", r.Title, 300).DateOrder("startDate", r.StartDate, "endDate", r.EndDate).ThrowIfInvalid();
        var award = await _db.Awards.SingleOrDefaultAsync(a => a.Id == r.AwardId, ct) ?? throw new NotFoundException("Award", r.AwardId);
        await _scope.EnsureProjectAsync(award.ProjectId, ct);
        if (!award.CanCreateContract)
            throw new DomainException(award.ContractId is not null
                ? "A contract already exists for this award."
                : "The award is not final or its conditions are not yet satisfied; an executable contract cannot be created.", "BR-004");

        var contract = new ContractEntity
        {
            ContractNumber = await _numbers.NextAsync(NumberPrefixes.Contract, ct), Title = r.Title, ProjectId = award.ProjectId,
            ProcurementId = award.ProcurementId, AwardId = award.Id, SupplierId = award.SupplierId, Source = ContractSource.Award,
            OriginalValue = award.Amount, StartDate = r.StartDate, OriginalEndDate = r.EndDate, CurrentEndDate = r.EndDate,
            ContractManagerUserId = r.ContractManagerUserId, ContractManagerName = r.ContractManagerName, PoReference = r.PoReference,
            Status = ContractStatus.PendingSignature
        };
        _db.Contracts.Add(contract);
        award.ContractId = contract.Id;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(contract.Id, ct);
    }

    /// <summary>FR-CON-001: authorised non-bid source requires an approved deviation/exception.</summary>
    public async Task<ContractDetailDto> CreateNonBidAsync(CreateNonBidContractRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("projectId", r.ProjectId).RequiredId("supplierId", r.SupplierId).RequiredId("exceptionId", r.ExceptionId)
            .Required("title", r.Title, 300).Positive("value", r.Value).DateOrder("startDate", r.StartDate, "endDate", r.EndDate).ThrowIfInvalid();
        await _scope.EnsureProjectAsync(r.ProjectId, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == r.ProjectId, ct);
        project.EnsureActive();
        var exception = await _db.ProcurementExceptions.AsNoTracking().SingleOrDefaultAsync(e => e.Id == r.ExceptionId, ct)
                        ?? throw new ValidationException("exceptionId", "Exception not found.");
        if (exception.Status != ApprovalState.Approved)
            throw new DomainException("A non-bid contract requires an approved deviation/exception.", "FR-CON-001");
        if (exception.Value is { } approvedValue && r.Value > approvedValue)
            throw new DomainException($"The contract value exceeds the approved exception value of R {approvedValue:N2}.", "FR-CON-009");
        _ = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == r.SupplierId, ct) ?? throw new ValidationException("supplierId", "Supplier not found.");

        var contract = new ContractEntity
        {
            ContractNumber = await _numbers.NextAsync(NumberPrefixes.Contract, ct), Title = r.Title, ProjectId = r.ProjectId, SupplierId = r.SupplierId,
            Source = ContractSource.NonBid, NonBidAuthority = $"{exception.Number}: {exception.Authority}", OriginalValue = r.Value,
            StartDate = r.StartDate, OriginalEndDate = r.EndDate, CurrentEndDate = r.EndDate, ContractManagerUserId = r.ContractManagerUserId,
            ContractManagerName = r.ContractManagerName, PoReference = r.PoReference, Status = ContractStatus.PendingSignature
        };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(contract.Id, ct);
    }

    public async Task<ContractDetailDto> UpdateAsync(Guid id, UpdateContractRequest r, CancellationToken ct)
    {
        new Validator().Required("title", r.Title, 300).ThrowIfInvalid();
        var c = await LoadAsync(id, ct);
        if (c.Status is ContractStatus.Closed or ContractStatus.Terminated) throw new DomainException("Closed contracts are read-only.", "FR-CON-013");
        c.Title = r.Title;
        c.ContractManagerUserId = r.ContractManagerUserId;
        c.ContractManagerName = r.ContractManagerName;
        c.PoReference = r.PoReference;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>FR-CON-005: records the signed agreement and activates the contract, creating its commitment (FR-FIN-003).</summary>
    public async Task<ContractDetailDto> SignAsync(Guid id, SignContractRequest r, CancellationToken ct)
    {
        var c = await LoadAsync(id, ct);
        if (c.Status != ContractStatus.PendingSignature && c.Status != ContractStatus.Draft)
            throw new DomainException($"Contract is {c.Status}; only unsigned contracts can be signed.", "FR-CON-005");
        var document = await _db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == r.SignedDocumentId, ct)
                       ?? throw new ValidationException("signedDocumentId", "Upload the signed agreement first.");
        if (document.ParentType != ParentTypes.Contract || document.ParentId != id)
            throw new ValidationException("signedDocumentId", "The document is not attached to this contract.");

        var doc = await _db.Documents.SingleAsync(d => d.Id == r.SignedDocumentId, ct);
        doc.SignatureStatus = "Signed";
        c.SignatureStatus = SignatureStatus.Signed;
        c.SignedDate = r.SignedDate;
        c.Status = ContractStatus.Active;

        _db.Commitments.Add(new Commitment
        {
            ProjectId = c.ProjectId, ContractId = c.Id, PoReference = c.PoReference, FinancialYear = Fy.For(r.SignedDate), Amount = c.OriginalValue,
            CommitmentDate = r.SignedDate, Source = CommitmentSource.Contract
        });
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ----- Obligations (FR-CON-003) -----
    public async Task<ObligationDto> SaveObligationAsync(Guid contractId, Guid? id, SaveObligationRequest r, CancellationToken ct)
    {
        new Validator().Required("description", r.Description, 2000).Required("ownerName", r.OwnerName, 200).ThrowIfInvalid();
        var c = await LoadAsync(contractId, ct);
        if (c.Status is ContractStatus.Closed or ContractStatus.Terminated) throw new DomainException("Closed contracts are read-only.", "FR-CON-013");
        ContractObligation o;
        if (id is null)
        {
            o = new ContractObligation { ContractId = contractId };
            _db.ContractObligations.Add(o);
        }
        else
        {
            o = await _db.ContractObligations.SingleOrDefaultAsync(x => x.Id == id && x.ContractId == contractId, ct) ?? throw new NotFoundException("Obligation", id);
        }
        o.Type = r.Type;
        o.Description = r.Description;
        o.OwnerUserId = r.OwnerUserId;
        o.OwnerName = r.OwnerName;
        o.DueDate = r.DueDate;
        o.EvidenceRequirement = r.EvidenceRequirement;
        o.KpiTarget = r.KpiTarget;
        if (r.Status is { } s) o.Status = s;
        await _db.SaveChangesAsync(ct);
        return ToDto(o, _clock.Today);
    }

    // ----- Deliverables (FR-CON-004, FR-EXE-006) -----
    public async Task<IReadOnlyList<DeliverableDto>> ListDeliverablesAsync(Guid? projectId, Guid? contractId, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.Deliverables.AsNoTracking().InScope(scope, d => d.ProjectId);
        if (projectId is { } pid) q = q.Where(d => d.ProjectId == pid);
        if (contractId is { } cid) q = q.Where(d => d.ContractId == cid);
        var rows = await q.OrderBy(d => d.DueDate).ToListAsync(ct);
        return await ToDtosAsync(rows, ct);
    }

    public async Task<DeliverableDto> SaveDeliverableAsync(Guid? id, SaveDeliverableRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("projectId", r.ProjectId).Required("name", r.Name, 300).NonNegative("payableAmount", r.PayableAmount).ThrowIfInvalid();
        await _scope.EnsureProjectAsync(r.ProjectId, ct);
        if (r.ContractId is { } cid)
        {
            var contract = await _db.Contracts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cid && c.ProjectId == r.ProjectId, ct)
                           ?? throw new ValidationException("contractId", "The contract does not belong to this project.");
            var others = (await _db.Deliverables.Where(d => d.ContractId == cid && d.Id != id).Select(d => d.PayableAmount).ToListAsync(ct)).Sum();
            if (others + r.PayableAmount > contract.RevisedValue)
                throw new DomainException($"Deliverable payable amounts (R {others + r.PayableAmount:N2}) would exceed the contract value R {contract.RevisedValue:N2}.", "FR-CON-009");
        }
        if (r.MilestoneId is { } mid && !await _db.WbsElements.AnyAsync(w => w.Id == mid && w.ProjectId == r.ProjectId, ct))
            throw new ValidationException("milestoneId", "The milestone does not belong to this project.");

        Deliverable d;
        if (id is null)
        {
            d = new Deliverable { Number = await _numbers.NextAsync(NumberPrefixes.Deliverable, ct) };
            _db.Deliverables.Add(d);
        }
        else
        {
            d = await _db.Deliverables.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Deliverable", id);
            if (d.AcceptanceStatus == AcceptanceStatus.Accepted) throw new DomainException("Accepted deliverables are locked.", "FR-CON-004");
        }
        d.ProjectId = r.ProjectId;
        d.ContractId = r.ContractId;
        d.MilestoneId = r.MilestoneId;
        d.Name = r.Name;
        d.Description = r.Description;
        d.DueDate = r.DueDate;
        d.PayableAmount = r.PayableAmount;
        d.AcceptanceCriteria = r.AcceptanceCriteria;
        d.EvidenceRequired = r.EvidenceRequired;
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { d }, ct))[0];
    }

    public async Task<DeliverableDto> SubmitDeliverableAsync(Guid id, CancellationToken ct)
    {
        var d = await LoadDeliverableAsync(id, ct);
        if (d.AcceptanceStatus is not (AcceptanceStatus.Pending or AcceptanceStatus.Rejected))
            throw new DomainException($"Deliverable is {d.AcceptanceStatus}.", "FR-CON-004");
        if (d.EvidenceRequired && !await _db.Evidence.AnyAsync(e => e.ParentType == ParentTypes.Deliverable && e.ParentId == id, ct))
            throw new DomainException("Attach the required evidence before submitting the deliverable.", "FR-EXE-006");
        d.AcceptanceStatus = AcceptanceStatus.Submitted;
        d.SubmittedAtUtc = _clock.UtcNow;
        _ledger.Record(nameof(Deliverable), d.Id, "SubmitDeliverable", d.ProjectId);
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { d }, ct))[0];
    }

    public async Task<DeliverableDto> DecideDeliverableAsync(Guid id, DeliverableDecisionRequest r, CancellationToken ct)
    {
        var d = await LoadDeliverableAsync(id, ct);
        if (d.AcceptanceStatus != AcceptanceStatus.Submitted) throw new DomainException("Only submitted deliverables can be accepted or rejected.", "FR-CON-004");
        if (!r.Accept && string.IsNullOrWhiteSpace(r.Reason)) throw new ValidationException("reason", "A reason is required when rejecting.");
        var userId = _user.UserId ?? throw new ForbiddenException();
        await _sod.EnsureAllowedAsync(nameof(Deliverable), d.Id, "AcceptDeliverable", userId, ct);

        if (r.Accept && d.EvidenceRequired)
        {
            var hasEvidence = await _db.Evidence.AnyAsync(e => e.ParentType == ParentTypes.Deliverable && e.ParentId == id
                && e.VerificationStatus != EvidenceStatus.Rejected, ct);
            if (!hasEvidence) throw new DomainException("Acceptance requires evidence where configured.", "FR-EXE-006");
        }

        d.AcceptanceStatus = r.Accept ? AcceptanceStatus.Accepted : AcceptanceStatus.Rejected;
        d.AcceptedAtUtc = r.Accept ? _clock.UtcNow : null;
        d.AcceptedBy = r.Accept ? _user.DisplayName ?? _user.Username : null;
        d.AcceptedByUserId = r.Accept ? userId : null;
        d.RejectionReason = r.Accept ? null : r.Reason;
        _ledger.Record(nameof(Deliverable), d.Id, "AcceptDeliverable", d.ProjectId);

        // Completing the linked milestone when its deliverable is accepted keeps execution in step.
        if (r.Accept && d.MilestoneId is { } mid)
        {
            var milestone = await _db.WbsElements.SingleAsync(w => w.Id == mid, ct);
            if (milestone.Status != Domain.Execution.WorkStatus.Completed)
            {
                milestone.RecordProgress(100, null, milestone.ActualStart ?? _clock.Today, _clock.Today);
            }
        }
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { d }, ct))[0];
    }

    public async Task<PaymentScheduleDto> SavePaymentScheduleAsync(Guid contractId, Guid? id, SavePaymentScheduleRequest r, CancellationToken ct)
    {
        new Validator().Required("description", r.Description, 500).Positive("amount", r.Amount).ThrowIfInvalid();
        var c = await LoadAsync(contractId, ct);
        if (r.DeliverableId is { } did && !await _db.Deliverables.AnyAsync(d => d.Id == did && d.ContractId == contractId, ct))
            throw new ValidationException("deliverableId", "Payment milestones must link to a deliverable of this contract.");
        var others = (await _db.PaymentScheduleItems.Where(p => p.ContractId == contractId && p.Id != id).Select(p => p.Amount).ToListAsync(ct)).Sum();
        if (others + r.Amount > c.RevisedValue)
            throw new DomainException($"The payment schedule (R {others + r.Amount:N2}) would exceed the contract ceiling R {c.RevisedValue:N2}.", "FR-CON-009");

        PaymentScheduleItem item;
        if (id is null)
        {
            item = new PaymentScheduleItem { ContractId = contractId };
            _db.PaymentScheduleItems.Add(item);
        }
        else
        {
            item = await _db.PaymentScheduleItems.SingleOrDefaultAsync(x => x.Id == id && x.ContractId == contractId, ct) ?? throw new NotFoundException("Payment milestone", id);
        }
        item.DeliverableId = r.DeliverableId;
        item.Description = r.Description;
        item.Amount = r.Amount;
        item.PlannedDate = r.PlannedDate;
        await _db.SaveChangesAsync(ct);
        return new PaymentScheduleDto(item.Id, item.ContractId, item.DeliverableId, item.Description, item.Amount, item.PlannedDate);
    }

    // ----- Variations & extensions (FR-CON-007/008) -----
    public async Task<VariationDto> SaveVariationAsync(Guid contractId, Guid? id, SaveVariationRequest r, CancellationToken ct)
    {
        new Validator().Required("description", r.Description, 4000).Required("reason", r.Reason, 4000).Range("days", r.Days, 0, 3650).ThrowIfInvalid();
        var c = await LoadAsync(contractId, ct);
        if (c.Status != ContractStatus.Active) throw new DomainException("Variations apply to active contracts.", "FR-CON-007");
        if ((r.IsExtension || r.Type is VariationType.Time or VariationType.Combined) && r.RevisedEndDate is null && r.Days <= 0)
            throw new ValidationException("revisedEndDate", "Provide the revised end date or number of days for a time variation/extension.");

        ContractVariation v;
        if (id is null)
        {
            v = new ContractVariation { Number = await _numbers.NextAsync(NumberPrefixes.Variation, ct), ContractId = contractId };
            _db.ContractVariations.Add(v);
        }
        else
        {
            v = await _db.ContractVariations.SingleOrDefaultAsync(x => x.Id == id && x.ContractId == contractId, ct) ?? throw new NotFoundException("Variation", id);
            if (v.Status != ApprovalState.Draft) throw new DomainException("Only draft variations can be edited.", "FR-CON-007");
        }
        v.Type = r.Type;
        v.IsExtension = r.IsExtension || r.Type == VariationType.Time;
        v.Description = r.Description;
        v.Reason = r.Reason;
        v.Amount = r.Type == VariationType.Time ? 0 : r.Amount;
        v.Days = r.Days;
        v.RevisedEndDate = r.RevisedEndDate ?? (r.Days > 0 ? c.CurrentEndDate.AddDays(r.Days) : null);
        v.ValueBefore = c.RevisedValue;
        v.EndDateBefore = c.CurrentEndDate;
        v.ChangeRequestId = r.ChangeRequestId;
        if (v.RevisedEndDate is { } end && end < c.StartDate)
            throw new ValidationException("revisedEndDate", "The revised end date cannot precede the contract start date.");
        await _db.SaveChangesAsync(ct);
        return ToDto(v);
    }

    public async Task<WorkflowInstanceDto> SubmitVariationAsync(Guid contractId, Guid variationId, CancellationToken ct)
    {
        var c = await LoadAsync(contractId, ct);
        var v = await _db.ContractVariations.SingleOrDefaultAsync(x => x.Id == variationId && x.ContractId == contractId, ct)
                ?? throw new NotFoundException("Variation", variationId);
        if (v.Status != ApprovalState.Draft) throw new DomainException("Only draft variations can be submitted.", "FR-CON-007");
        v.Status = ApprovalState.Pending;
        var value = Math.Abs(v.Amount);
        var instance = await _workflow.StartAsync(new StartWorkflowRequest(VariationWorkflow, nameof(ContractVariation), v.Id, v.Number,
            $"{(v.IsExtension ? "Extension" : "Variation")} of {c.ContractNumber}: {v.Description}", value, c.ProjectId, $"contracts/{c.Id}"), ct);
        v.WorkflowInstanceId = instance.Id;
        await _db.SaveChangesAsync(ct);
        return await _workflow.GetAsync(instance.Id, ct);
    }

    // ----- Performance & breaches (FR-CON-010/011) -----
    public async Task<ReviewDto> AddReviewAsync(Guid contractId, SaveReviewRequest r, CancellationToken ct)
    {
        new Validator().Required("period", r.Period, 50).ThrowIfInvalid();
        var c = await LoadAsync(contractId, ct);
        var (overall, rating) = ContractPerformanceReview.Calculate(r.QualityScore, r.TimelinessScore, r.ComplianceScore);
        if (await _db.ContractPerformanceReviews.AnyAsync(x => x.ContractId == contractId && x.Period == r.Period, ct))
            throw new ConflictException($"A performance review for {r.Period} already exists; assessments are retained by period.", "FR-CON-010");
        var review = new ContractPerformanceReview
        {
            ContractId = contractId, SupplierId = c.SupplierId, Period = r.Period, ReviewDate = r.ReviewDate, QualityScore = r.QualityScore,
            TimelinessScore = r.TimelinessScore, ComplianceScore = r.ComplianceScore, OverallScore = overall, Rating = rating, Comments = r.Comments,
            ReviewedBy = _user.DisplayName ?? _user.Username
        };
        _db.ContractPerformanceReviews.Add(review);
        var existing = await _db.ContractPerformanceReviews.Where(x => x.ContractId == contractId).Select(x => x.OverallScore).ToListAsync(ct);
        existing.Add(overall);
        c.PerformanceRating = Math.Round(existing.Average(), 2);
        await _db.SaveChangesAsync(ct);
        return ToDto(review);
    }

    public async Task<BreachDto> SaveBreachAsync(Guid contractId, Guid? id, SaveBreachRequest r, CancellationToken ct)
    {
        new Validator().Required("description", r.Description, 4000).NonNegative("penaltyAmount", r.PenaltyAmount).ThrowIfInvalid();
        await LoadAsync(contractId, ct);
        ContractBreach b;
        if (id is null)
        {
            b = new ContractBreach { ContractId = contractId };
            _db.ContractBreaches.Add(b);
        }
        else
        {
            b = await _db.ContractBreaches.SingleOrDefaultAsync(x => x.Id == id && x.ContractId == contractId, ct) ?? throw new NotFoundException("Breach", id);
        }
        b.Description = r.Description;
        b.Severity = r.Severity;
        b.IdentifiedOn = r.IdentifiedOn;
        b.NoticeDate = r.NoticeDate;
        b.NoticeReference = r.NoticeReference;
        b.Remedy = r.Remedy;
        b.RemedyDueDate = r.RemedyDueDate;
        b.PenaltyAmount = r.PenaltyAmount;
        if (r.Status is { } s) b.Status = s;
        else if (r.NoticeDate is not null && b.Status == BreachStatus.Open) b.Status = BreachStatus.NoticeIssued;
        await _db.SaveChangesAsync(ct);
        return ToDto(b);
    }

    // ----- Close-out (FR-CON-013, BR-010) -----
    public async Task<CloseOutResultDto> CloseOutChecksAsync(Guid id, CancellationToken ct)
    {
        var c = await LoadAsync(id, ct, tracking: false);
        var checks = new List<CloseOutCheckDto>();
        var pendingDeliverables = await _db.Deliverables.CountAsync(d => d.ContractId == id && d.AcceptanceStatus != AcceptanceStatus.Accepted, ct);
        checks.Add(new CloseOutCheckDto("Final deliverables accepted", pendingDeliverables == 0, $"{pendingDeliverables} deliverable(s) not accepted"));
        var openObligations = await _db.ContractObligations.CountAsync(o => o.ContractId == id && o.Status == ObligationStatus.Open, ct);
        checks.Add(new CloseOutCheckDto("Obligations resolved", openObligations == 0, $"{openObligations} open obligation(s)"));
        var openBreaches = await _db.ContractBreaches.CountAsync(b => b.ContractId == id && b.Status != BreachStatus.Remedied && b.Status != BreachStatus.Closed, ct);
        checks.Add(new CloseOutCheckDto("No open breaches", openBreaches == 0, $"{openBreaches} open breach(es)"));
        var unsettled = await _db.Invoices.CountAsync(i => i.ContractId == id && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Rejected
            && i.Status != InvoiceStatus.Cancelled, ct);
        checks.Add(new CloseOutCheckDto("Financial settlement complete", unsettled == 0, $"{unsettled} invoice(s) not settled"));
        var pendingVariations = await _db.ContractVariations.CountAsync(v => v.ContractId == id && v.Status == ApprovalState.Pending, ct);
        checks.Add(new CloseOutCheckDto("No pending variations", pendingVariations == 0, $"{pendingVariations} variation(s) pending"));
        var signed = await _db.Documents.AnyAsync(d => d.ParentType == ParentTypes.Contract && d.ParentId == id && d.SignatureStatus == "Signed", ct);
        checks.Add(new CloseOutCheckDto("Signed agreement on file", signed, signed ? "Signed agreement stored" : "No signed agreement"));
        var finalReview = await _db.ContractPerformanceReviews.AnyAsync(r => r.ContractId == id, ct);
        checks.Add(new CloseOutCheckDto("Final performance assessment", finalReview, finalReview ? "Performance assessed" : "No performance review"));
        return new CloseOutResultDto(checks.All(x => x.Met), checks);
    }

    public async Task<ContractDetailDto> CloseAsync(Guid id, CloseContractRequest r, CancellationToken ct)
    {
        new Validator().Required("closureNotes", r.ClosureNotes, 4000).ThrowIfInvalid();
        var result = await CloseOutChecksAsync(id, ct);
        var c = await LoadAsync(id, ct);
        if (c.Status is ContractStatus.Closed or ContractStatus.Terminated) throw new DomainException("The contract is already closed.", "FR-CON-013");
        if (!result.CanClose)
        {
            if (string.IsNullOrWhiteSpace(r.ApprovedExceptionReference))
                throw new DomainException("The contract cannot close with unresolved mandatory items: " +
                    string.Join("; ", result.Checks.Where(x => !x.Met).Select(x => x.Detail)) + ".", "BR-010");
            var approved = await _db.ProcurementExceptions.AnyAsync(e => e.Number == r.ApprovedExceptionReference && e.Status == ApprovalState.Approved, ct);
            if (!approved) throw new DomainException("The exception reference is not an approved exception.", "BR-011");
            _audit.Write("Contracts", "Contract", id.ToString(), "ClosedWithException",
                new { Unmet = result.Checks.Where(x => !x.Met).Select(x => x.Item) }, r.ApprovedExceptionReference);
        }
        c.Status = ContractStatus.Closed;
        c.ClosedAtUtc = _clock.UtcNow;
        c.ClosureNotes = r.ClosureNotes;
        c.ClosureExceptionReference = r.ApprovedExceptionReference;

        // Release any unspent commitment on closure.
        var paid = (await _db.Invoices.Where(i => i.ContractId == id && i.Status == InvoiceStatus.Paid).Select(i => i.Amount).ToListAsync(ct)).Sum();
        var commitments = await _db.Commitments.Where(x => x.ContractId == id && !x.IsReleased).ToListAsync(ct);
        var committed = commitments.Sum(x => x.Amount);
        if (committed > paid)
        {
            foreach (var cm in commitments) cm.IsReleased = true;
            _db.Commitments.Add(new Commitment
            {
                ProjectId = c.ProjectId, ContractId = c.Id, PoReference = c.PoReference, FinancialYear = Fy.For(_clock.Today), Amount = paid,
                CommitmentDate = _clock.Today, Source = CommitmentSource.Contract
            });
        }
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ----- Dashboard (FR-CON-015) -----
    public async Task<ContractDashboardDto> DashboardAsync(Guid? projectId, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.Contracts.AsNoTracking().InScope(scope, c => c.ProjectId);
        if (projectId is { } pid) q = q.Where(c => c.ProjectId == pid);
        var rows = await q.ToListAsync(ct);
        var items = await ToListItemsAsync(rows, ct);
        var active = items.Where(i => i.Status == nameof(ContractStatus.Active)).ToList();
        var ids = rows.Select(r => r.Id).ToList();
        var pendingVariations = await _db.ContractVariations.CountAsync(v => ids.Contains(v.ContractId) && v.Status == ApprovalState.Pending, ct);
        var expiring = active.Where(i => i.DaysToExpiry is >= 0 and <= 90).OrderBy(i => i.DaysToExpiry).ToList();
        var exceptions = items.Where(i => i.OpenBreaches > 0 || (i.Status == nameof(ContractStatus.Active) && i.DaysToExpiry < 0)
                                          || i.Committed > i.RevisedValue || (i.PerformanceRating is < 2.5m)).ToList();
        var rated = items.Where(i => i.PerformanceRating is not null).ToList();
        return new ContractDashboardDto(active.Count, active.Sum(i => i.RevisedValue), active.Sum(i => i.OriginalValue), active.Sum(i => i.ApprovedVariations),
            expiring.Count, items.Count(i => i.Status == nameof(ContractStatus.Expired) || (i.Status == nameof(ContractStatus.Active) && i.DaysToExpiry < 0)),
            pendingVariations, items.Sum(i => i.OpenBreaches), rated.Count == 0 ? null : Math.Round(rated.Average(i => i.PerformanceRating!.Value), 2),
            expiring.Take(20).ToList(), exceptions.Take(20).ToList(),
            items.GroupBy(i => i.Status).Select(g => new NameValue(g.Key, g.Count())).ToList(),
            rated.GroupBy(i => i.PerformanceRating switch { >= 4.5m => "Excellent", >= 3.5m => "Good", >= 2.5m => "Satisfactory", >= 1.5m => "Poor", _ => "Unacceptable" })
                .Select(g => new NameValue(g.Key, g.Count())).ToList());
    }

    public async Task<decimal> CommittedAsync(Guid contractId, CancellationToken ct) =>
        (await _db.Commitments.Where(c => c.ContractId == contractId && !c.IsReleased).Select(c => c.Amount).ToListAsync(ct)).Sum();

    // ----- helpers -----
    private async Task<ContractEntity> LoadAsync(Guid id, CancellationToken ct, bool tracking = true)
    {
        var q = tracking ? _db.Contracts : _db.Contracts.AsNoTracking();
        var c = await q.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Contract", id);
        await _scope.EnsureProjectAsync(c.ProjectId, ct);
        return c;
    }

    private async Task<Deliverable> LoadDeliverableAsync(Guid id, CancellationToken ct)
    {
        var d = await _db.Deliverables.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Deliverable", id);
        await _scope.EnsureProjectAsync(d.ProjectId, ct);
        return d;
    }

    private async Task<List<ContractListItemDto>> ToListItemsAsync(IReadOnlyCollection<ContractEntity> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var supplierIds = rows.Select(r => r.SupplierId).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = p.ProjectNumber ?? p.DraftReference }).ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.LegalName, ct);
        var commitments = await _db.Commitments.AsNoTracking().Where(c => c.ContractId != null && ids.Contains(c.ContractId.Value) && !c.IsReleased)
            .Select(c => new { ContractId = c.ContractId!.Value, c.Amount }).ToListAsync(ct);
        var invoices = await _db.Invoices.AsNoTracking().Where(i => ids.Contains(i.ContractId) && i.Status != InvoiceStatus.Rejected && i.Status != InvoiceStatus.Cancelled)
            .Select(i => new { i.ContractId, i.Amount, i.Status }).ToListAsync(ct);
        var breaches = await _db.ContractBreaches.AsNoTracking().Where(b => ids.Contains(b.ContractId) && b.Status != BreachStatus.Remedied && b.Status != BreachStatus.Closed)
            .Select(b => b.ContractId).ToListAsync(ct);
        var today = _clock.Today;
        return rows.Select(c => new ContractListItemDto(c.Id, c.ContractNumber, c.Title, c.ProjectId, projects.GetValueOrDefault(c.ProjectId, "?"),
            c.SupplierId, suppliers.GetValueOrDefault(c.SupplierId, "?"), c.OriginalValue, c.ApprovedVariations, c.RevisedValue,
            commitments.Where(x => x.ContractId == c.Id).Sum(x => x.Amount), invoices.Where(i => i.ContractId == c.Id).Sum(i => i.Amount),
            invoices.Where(i => i.ContractId == c.Id && i.Status == InvoiceStatus.Paid).Sum(i => i.Amount), c.StartDate, c.OriginalEndDate,
            c.CurrentEndDate, c.CurrentEndDate.DayNumber - today.DayNumber, c.Status.ToString(), c.SignatureStatus.ToString(), c.ContractManagerName,
            c.PerformanceRating, breaches.Count(b => b == c.Id), c.Source.ToString())).ToList();
    }

    private async Task<List<DeliverableDto>> ToDtosAsync(IReadOnlyCollection<Deliverable> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var contractIds = rows.Where(r => r.ContractId != null).Select(r => r.ContractId!.Value).Distinct().ToList();
        var contracts = await _db.Contracts.AsNoTracking().Where(c => contractIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.ContractNumber, ct);
        var evidence = await _db.Evidence.AsNoTracking().Where(e => e.ParentType == ParentTypes.Deliverable && ids.Contains(e.ParentId))
            .Select(e => e.ParentId).ToListAsync(ct);
        var invoiced = await _db.Invoices.AsNoTracking()
            .Where(i => i.DeliverableId != null && ids.Contains(i.DeliverableId.Value) && i.Status != InvoiceStatus.Rejected && i.Status != InvoiceStatus.Cancelled)
            .Select(i => new { DeliverableId = i.DeliverableId!.Value, i.Amount }).ToListAsync(ct);
        var today = _clock.Today;
        return rows.Select(d => new DeliverableDto(d.Id, d.Number, d.ProjectId, d.ContractId, d.ContractId is { } cid ? contracts.GetValueOrDefault(cid) : null,
            d.MilestoneId, d.Name, d.Description, d.DueDate, d.PayableAmount, invoiced.Where(i => i.DeliverableId == d.Id).Sum(i => i.Amount),
            d.AcceptanceCriteria, d.EvidenceRequired, d.AcceptanceStatus.ToString(), d.SubmittedAtUtc, d.AcceptedAtUtc, d.AcceptedBy, d.RejectionReason,
            evidence.Count(e => e == d.Id), d.AcceptanceStatus != AcceptanceStatus.Accepted && d.DueDate < today, d.Version)).ToList();
    }

    private static ObligationDto ToDto(ContractObligation o, DateOnly today) =>
        new(o.Id, o.ContractId, o.Type.ToString(), o.Description, o.OwnerUserId, o.OwnerName, o.DueDate, o.EvidenceRequirement, o.KpiTarget,
            o.Status.ToString(), o.Status == ObligationStatus.Open && o.DueDate < today, o.Version);

    internal static VariationDto ToDto(ContractVariation v) =>
        new(v.Id, v.Number, v.ContractId, v.Type.ToString(), v.IsExtension, v.Description, v.Reason, v.Amount, v.Days, v.RevisedEndDate, v.ValueBefore,
            v.ValueAfter, v.EndDateBefore, v.EndDateAfter, v.Status.ToString(), v.DecidedAtUtc, v.DecidedBy, v.WorkflowInstanceId, v.CreatedBy, v.CreatedAtUtc);

    private static ReviewDto ToDto(ContractPerformanceReview r) =>
        new(r.Id, r.ContractId, r.Period, r.ReviewDate, r.QualityScore, r.TimelinessScore, r.ComplianceScore, r.OverallScore, r.Rating, r.Comments, r.ReviewedBy);

    private static BreachDto ToDto(ContractBreach b) =>
        new(b.Id, b.ContractId, b.Description, b.Severity.ToString(), b.IdentifiedOn, b.NoticeDate, b.NoticeReference, b.Remedy, b.RemedyDueDate,
            b.PenaltyAmount, b.Status.ToString(), b.IsOpen, b.Version);
}

/// <summary>Variation/extension approval completes → revised value/end date; originals stay visible (FR-CON-007/008, BR-005).</summary>
public sealed class ContractVariationApprovalHandler : IWorkflowCompletionHandler
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;

    public ContractVariationApprovalHandler(ITetaDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public string EntityType => nameof(ContractVariation);

    public async Task OnCompletedAsync(WorkflowInstance instance, WorkflowState outcome, string? comment, CancellationToken ct)
    {
        var v = await _db.ContractVariations.SingleAsync(x => x.Id == instance.EntityId, ct);
        v.DecidedAtUtc = _clock.UtcNow;
        v.DecidedBy = instance.Tasks.Where(t => t.DecidedAtUtc != null).OrderByDescending(t => t.DecidedAtUtc).FirstOrDefault()?.DecidedBy;
        if (outcome != WorkflowState.Approved)
        {
            v.Status = outcome == WorkflowState.Rejected ? ApprovalState.Rejected : ApprovalState.Draft;
            return;
        }

        var contract = await _db.Contracts.SingleAsync(c => c.Id == v.ContractId, ct);
        contract.ApplyApprovedVariation(v.Amount, v.RevisedEndDate);
        v.Status = ApprovalState.Approved;
        v.ValueAfter = contract.RevisedValue;
        v.EndDateAfter = contract.CurrentEndDate;
        if (contract.Status == ContractStatus.Expired && contract.CurrentEndDate >= _clock.Today) contract.Status = ContractStatus.Active;

        if (v.Amount != 0)
        {
            _db.Commitments.Add(new Commitment
            {
                ProjectId = contract.ProjectId, ContractId = contract.Id, PoReference = contract.PoReference, FinancialYear = Fy.For(_clock.Today),
                Amount = v.Amount, CommitmentDate = _clock.Today, Source = CommitmentSource.Contract
            });
        }
    }
}
