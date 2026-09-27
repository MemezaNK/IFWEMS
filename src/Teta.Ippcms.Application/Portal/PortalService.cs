using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Finance;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Scm;
using Teta.Ippcms.Domain.Suppliers;

namespace Teta.Ippcms.Application.Portal;

public sealed record PortalProfileDto(Guid SupplierId, string SupplierNumber, string LegalName, string? TradingName, string Status, bool IsImplementingPartner,
    string? Email, string? Phone, bool CsdVerified, DateOnly? TaxClearanceExpiry);
public sealed record PortalBidDto(Guid BidId, string ProcurementNumber, string ProcurementTitle, string BidReference, DateTime ReceivedAtUtc, decimal BidAmount,
    string ProcurementStatus, string Outcome, IReadOnlyList<PortalCommunicationDto> Communications);
public sealed record PortalCommunicationDto(string Type, DateOnly Date, string Subject, string Details, string? Outcome);
public sealed record PortalContractDto(Guid Id, string ContractNumber, string Title, string Status, decimal OriginalValue, decimal RevisedValue, DateOnly StartDate,
    DateOnly CurrentEndDate, string? ContractManager, int OpenDeliverables, decimal Invoiced, decimal Paid);
public sealed record PortalDeliverableDto(Guid Id, string Number, Guid? ContractId, string? ContractNumber, string Name, string? Description, DateOnly DueDate,
    decimal PayableAmount, string? AcceptanceCriteria, bool EvidenceRequired, string AcceptanceStatus, DateTime? SubmittedAtUtc, DateTime? AcceptedAtUtc,
    string? RejectionReason, int EvidenceCount);
public sealed record PortalInvoiceDto(Guid Id, string Number, string SupplierInvoiceNumber, string ContractNumber, DateOnly InvoiceDate, decimal Amount,
    decimal VatAmount, string Status, string? RejectionReason, decimal Paid, DateOnly? PaidOn);
public sealed record SubmitDeliverableRequest(string? Note);
public sealed record PortalSummaryDto(PortalProfileDto Profile, int ActiveContracts, int DeliverablesDue, int DeliverablesRejected, int InvoicesInProgress,
    decimal PaidThisYear);

public interface IPortalService
{
    Task<PortalSummaryDto> SummaryAsync(CancellationToken ct);
    Task<IReadOnlyList<PortalBidDto>> MyBidsAsync(CancellationToken ct);
    Task<IReadOnlyList<PortalContractDto>> MyContractsAsync(CancellationToken ct);
    Task<IReadOnlyList<PortalDeliverableDto>> MyDeliverablesAsync(Guid? contractId, CancellationToken ct);
    Task<PortalDeliverableDto> SubmitDeliverableAsync(Guid deliverableId, SubmitDeliverableRequest request, CancellationToken ct);
    Task<IReadOnlyList<PortalInvoiceDto>> MyInvoicesAsync(CancellationToken ct);
    Task<PortalInvoiceDto> SubmitInvoiceAsync(RegisterInvoiceRequest request, string? idempotencyKey, CancellationToken ct);
}

/// <summary>
/// Supplier / implementing-partner portal (SRS §10, §13): exposes only the linked supplier's own
/// submissions, contracts, deliverables and invoices. Bid scores and competitors are never disclosed.
/// </summary>
public sealed class PortalService : IPortalService
{
    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IFinanceService _finance;
    private readonly IAuditWriter _audit;

    public PortalService(ITetaDbContext db, ICurrentUser user, IClock clock, IFinanceService finance, IAuditWriter audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _finance = finance;
        _audit = audit;
    }

    private async Task<Supplier> MySupplierAsync(CancellationToken ct)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Sign in required.");
        var supplierId = await _db.SupplierUsers.AsNoTracking().Where(s => s.UserId == userId).Select(s => (Guid?)s.SupplierId).FirstOrDefaultAsync(ct)
                         ?? throw new ForbiddenException("Your account is not linked to a supplier. Contact the SCM office.");
        var supplier = await _db.Suppliers.AsNoTracking().SingleAsync(s => s.Id == supplierId, ct);
        if (supplier.Status is SupplierStatus.Restricted or SupplierStatus.Inactive)
            throw new ForbiddenException("Portal access for this supplier is suspended.");
        return supplier;
    }

    public async Task<PortalSummaryDto> SummaryAsync(CancellationToken ct)
    {
        var s = await MySupplierAsync(ct);
        var contracts = await _db.Contracts.AsNoTracking().Where(c => c.SupplierId == s.Id).Select(c => new { c.Id, c.Status }).ToListAsync(ct);
        var contractIds = contracts.Select(c => c.Id).ToList();
        var horizon = _clock.Today.AddDays(30);
        var deliverables = await _db.Deliverables.AsNoTracking().Where(d => d.ContractId != null && contractIds.Contains(d.ContractId.Value))
            .Select(d => new { d.AcceptanceStatus, d.DueDate }).ToListAsync(ct);
        var invoices = await _db.Invoices.AsNoTracking().Where(i => i.SupplierId == s.Id).Select(i => new { i.Id, i.Status }).ToListAsync(ct);
        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var fyStart = new DateOnly(_clock.Today.Month >= 4 ? _clock.Today.Year : _clock.Today.Year - 1, 4, 1);
        var paid = (await _db.Payments.AsNoTracking().Where(p => invoiceIds.Contains(p.InvoiceId) && p.Status == PaymentStatus.Paid && p.PaymentDate >= fyStart)
            .Select(p => p.Amount).ToListAsync(ct)).Sum();

        return new PortalSummaryDto(ToProfile(s), contracts.Count(c => c.Status == ContractStatus.Active),
            deliverables.Count(d => d.AcceptanceStatus is AcceptanceStatus.Pending && d.DueDate <= horizon),
            deliverables.Count(d => d.AcceptanceStatus == AcceptanceStatus.Rejected),
            invoices.Count(i => i.Status is not (InvoiceStatus.Paid or InvoiceStatus.Rejected or InvoiceStatus.Cancelled)), paid);
    }

    private static PortalProfileDto ToProfile(Supplier s) => new(s.Id, s.SupplierNumber, s.LegalName, s.TradingName, s.Status.ToString(), s.IsImplementingPartner,
        s.Email, s.Phone, s.CsdVerified, s.TaxClearanceExpiry);

    public async Task<IReadOnlyList<PortalBidDto>> MyBidsAsync(CancellationToken ct)
    {
        var s = await MySupplierAsync(ct);
        var bids = await (from b in _db.Bids.AsNoTracking()
                          join p in _db.Procurements.AsNoTracking() on b.ProcurementId equals p.Id
                          where b.SupplierId == s.Id
                          orderby b.ReceivedAtUtc descending
                          select new { Bid = b, p.Number, p.Title, p.Status }).ToListAsync(ct);
        var bidIds = bids.Select(b => b.Bid.Id).ToList();
        var procurementIds = bids.Select(b => b.Bid.ProcurementId).ToList();
        var awards = await _db.Awards.AsNoTracking().Where(a => procurementIds.Contains(a.ProcurementId) && a.Status != AwardStatus.Cancelled)
            .Select(a => new { a.ProcurementId, a.BidId }).ToListAsync(ct);
        var comms = await _db.BidderCommunications.AsNoTracking()
            .Where(c => c.SupplierId == s.Id || (c.BidId != null && bidIds.Contains(c.BidId.Value))).ToListAsync(ct);

        return bids.Select(x =>
        {
            var award = awards.FirstOrDefault(a => a.ProcurementId == x.Bid.ProcurementId);
            var outcome = x.Status == ProcurementStatus.Cancelled ? "Procurement cancelled"
                : award is null ? (x.Bid.IsLate ? "Late - not considered" : "Under evaluation")
                : award.BidId == x.Bid.Id ? "Successful" : "Unsuccessful";
            return new PortalBidDto(x.Bid.Id, x.Number, x.Title, x.Bid.BidReference, x.Bid.ReceivedAtUtc, x.Bid.BidAmount, x.Status.ToString(), outcome,
                comms.Where(c => c.ProcurementId == x.Bid.ProcurementId).OrderBy(c => c.Date)
                    .Select(c => new PortalCommunicationDto(c.Type.ToString(), c.Date, c.Subject, c.Details, c.Outcome)).ToList());
        }).ToList();
    }

    public async Task<IReadOnlyList<PortalContractDto>> MyContractsAsync(CancellationToken ct)
    {
        var s = await MySupplierAsync(ct);
        var contracts = await _db.Contracts.AsNoTracking().Where(c => c.SupplierId == s.Id && c.Status != ContractStatus.Draft)
            .OrderByDescending(c => c.StartDate).ToListAsync(ct);
        var ids = contracts.Select(c => c.Id).ToList();
        var deliverables = await _db.Deliverables.AsNoTracking().Where(d => d.ContractId != null && ids.Contains(d.ContractId.Value))
            .Select(d => new { d.ContractId, d.AcceptanceStatus }).ToListAsync(ct);
        var invoices = await _db.Invoices.AsNoTracking().Where(i => ids.Contains(i.ContractId) && i.Status != InvoiceStatus.Rejected && i.Status != InvoiceStatus.Cancelled)
            .Select(i => new { i.Id, i.ContractId, i.Amount }).ToListAsync(ct);
        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var payments = await _db.Payments.AsNoTracking().Where(p => invoiceIds.Contains(p.InvoiceId) && p.Status == PaymentStatus.Paid)
            .Select(p => new { p.InvoiceId, p.Amount }).ToListAsync(ct);

        return contracts.Select(c =>
        {
            var cInvoices = invoices.Where(i => i.ContractId == c.Id).ToList();
            var cInvoiceIds = cInvoices.Select(i => i.Id).ToHashSet();
            return new PortalContractDto(c.Id, c.ContractNumber, c.Title, c.Status.ToString(), c.OriginalValue, c.RevisedValue, c.StartDate, c.CurrentEndDate,
                c.ContractManagerName, deliverables.Count(d => d.ContractId == c.Id && d.AcceptanceStatus != AcceptanceStatus.Accepted),
                cInvoices.Sum(i => i.Amount), payments.Where(p => cInvoiceIds.Contains(p.InvoiceId)).Sum(p => p.Amount));
        }).ToList();
    }

    public async Task<IReadOnlyList<PortalDeliverableDto>> MyDeliverablesAsync(Guid? contractId, CancellationToken ct)
    {
        var s = await MySupplierAsync(ct);
        var rows = await (from d in _db.Deliverables.AsNoTracking()
                          join c in _db.Contracts.AsNoTracking() on d.ContractId equals (Guid?)c.Id
                          where c.SupplierId == s.Id && (contractId == null || c.Id == contractId)
                          orderby d.DueDate
                          select new { d, c.ContractNumber }).ToListAsync(ct);
        return await ToDeliverableDtosAsync(rows.Select(r => (r.d, (string?)r.ContractNumber)).ToList(), ct);
    }

    private async Task<List<PortalDeliverableDto>> ToDeliverableDtosAsync(List<(Deliverable D, string? ContractNumber)> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.D.Id).ToList();
        var evidence = await _db.Evidence.AsNoTracking().Where(e => e.ParentType == Domain.Common.ParentTypes.Deliverable && ids.Contains(e.ParentId))
            .Select(e => e.ParentId).ToListAsync(ct);
        return rows.Select(r => new PortalDeliverableDto(r.D.Id, r.D.Number, r.D.ContractId, r.ContractNumber, r.D.Name, r.D.Description, r.D.DueDate,
            r.D.PayableAmount, r.D.AcceptanceCriteria, r.D.EvidenceRequired, r.D.AcceptanceStatus.ToString(), r.D.SubmittedAtUtc, r.D.AcceptedAtUtc,
            r.D.RejectionReason, evidence.Count(e => e == r.D.Id))).ToList();
    }

    /// <summary>Supplier submits a deliverable for acceptance; evidence must be uploaded first when required.</summary>
    public async Task<PortalDeliverableDto> SubmitDeliverableAsync(Guid deliverableId, SubmitDeliverableRequest request, CancellationToken ct)
    {
        new Validator().Optional("note", request.Note, 1000).ThrowIfInvalid();
        var s = await MySupplierAsync(ct);
        var deliverable = await _db.Deliverables.SingleOrDefaultAsync(d => d.Id == deliverableId, ct) ?? throw new NotFoundException("Deliverable", deliverableId);
        var contract = deliverable.ContractId is { } cid ? await _db.Contracts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cid, ct) : null;
        if (contract is null || contract.SupplierId != s.Id) throw new NotFoundException("Deliverable", deliverableId);
        if (contract.Status != ContractStatus.Active) throw new DomainException("Deliverables can only be submitted on active contracts.", "FR-CON-003");
        if (deliverable.AcceptanceStatus is AcceptanceStatus.Accepted or AcceptanceStatus.Submitted)
            throw new DomainException($"This deliverable is already {deliverable.AcceptanceStatus}.", "FR-CON-003");
        if (deliverable.EvidenceRequired
            && !await _db.Evidence.AnyAsync(e => e.ParentType == Domain.Common.ParentTypes.Deliverable && e.ParentId == deliverable.Id, ct))
            throw new DomainException("Upload the required evidence before submitting this deliverable.", "BR-009");

        deliverable.AcceptanceStatus = AcceptanceStatus.Submitted;
        deliverable.SubmittedAtUtc = _clock.UtcNow;
        deliverable.RejectionReason = null;
        _audit.Write("Contracts", nameof(Deliverable), deliverable.Id.ToString(), "PortalSubmit", new { deliverable.Number, request.Note });
        await _db.SaveChangesAsync(ct);
        return (await ToDeliverableDtosAsync(new List<(Deliverable, string?)> { (deliverable, contract.ContractNumber) }, ct))[0];
    }

    public async Task<IReadOnlyList<PortalInvoiceDto>> MyInvoicesAsync(CancellationToken ct)
    {
        var s = await MySupplierAsync(ct);
        var invoices = await (from i in _db.Invoices.AsNoTracking()
                              join c in _db.Contracts.AsNoTracking() on i.ContractId equals c.Id
                              where i.SupplierId == s.Id
                              orderby i.InvoiceDate descending
                              select new { i, c.ContractNumber }).ToListAsync(ct);
        var ids = invoices.Select(x => x.i.Id).ToList();
        var payments = await _db.Payments.AsNoTracking().Where(p => ids.Contains(p.InvoiceId) && p.Status == PaymentStatus.Paid).ToListAsync(ct);
        return invoices.Select(x =>
        {
            var paid = payments.Where(p => p.InvoiceId == x.i.Id).ToList();
            return new PortalInvoiceDto(x.i.Id, x.i.Number, x.i.SupplierInvoiceNumber, x.ContractNumber, x.i.InvoiceDate, x.i.Amount, x.i.VatAmount,
                x.i.Status.ToString(), x.i.RejectionReason, paid.Sum(p => p.Amount), paid.Select(p => (DateOnly?)p.PaymentDate).Max());
        }).ToList();
    }

    public async Task<PortalInvoiceDto> SubmitInvoiceAsync(RegisterInvoiceRequest request, string? idempotencyKey, CancellationToken ct)
    {
        var s = await MySupplierAsync(ct);
        if (!await _db.Contracts.AnyAsync(c => c.Id == request.ContractId && c.SupplierId == s.Id && c.Status == ContractStatus.Active, ct))
            throw new NotFoundException("Contract", request.ContractId);
        var dto = await _finance.RegisterInvoiceAsync(request with { ReceivedDate = _clock.Today }, idempotencyKey, ct);
        return (await MyInvoicesAsync(ct)).Single(i => i.Id == dto.Id);
    }
}
