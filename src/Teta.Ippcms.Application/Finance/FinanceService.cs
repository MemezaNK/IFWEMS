using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Projects;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.Application.Finance;

// ---------- DTOs ----------
public sealed record FinancialViewDto(Guid? ProjectId, string Scope, decimal ApprovedBudget, decimal RevisedBudget, decimal Committed, decimal Actual,
    decimal Accrued, decimal Invoiced, decimal Paid, decimal EstimateAtCompletion, decimal Variance, decimal VariancePercent, decimal Available,
    IReadOnlyList<CashflowPoint> Cashflow, IReadOnlyList<ProjectFinancialRow> Projects);
public sealed record CashflowPoint(string Period, decimal Planned, decimal Actual);
public sealed record ProjectFinancialRow(Guid ProjectId, string Reference, string Name, decimal Budget, decimal Committed, decimal Actual,
    decimal EstimateAtCompletion, decimal VariancePercent, string Health);

public sealed record CommitmentDto(Guid Id, Guid ProjectId, Guid? ContractId, string? PoReference, string FinancialYear, decimal Amount,
    DateOnly CommitmentDate, string Source, bool IsReleased, string? ErpReference);
public sealed record SaveCommitmentRequest(Guid ProjectId, Guid? ContractId, string PoReference, decimal Amount, DateOnly CommitmentDate, string? ErpReference);

public sealed record InvoiceDto(Guid Id, string Number, string SupplierInvoiceNumber, Guid ContractId, string ContractNumber, Guid? DeliverableId,
    string? DeliverableName, Guid SupplierId, string SupplierName, Guid ProjectId, string ProjectReference, string? PoReference, DateOnly InvoiceDate,
    DateOnly ReceivedDate, decimal Amount, decimal VatAmount, string Status, bool PotentialDuplicate, IReadOnlyList<string> ValidationMessages,
    DateTime? CertifiedAtUtc, string? CertifiedBy, string? RejectionReason, IReadOnlyList<PaymentDto> Payments, Guid? WorkflowInstanceId, long Version);
public sealed record RegisterInvoiceRequest(Guid ContractId, Guid? DeliverableId, string SupplierInvoiceNumber, string? PoReference, DateOnly InvoiceDate,
    DateOnly? ReceivedDate, decimal Amount, decimal VatAmount);
public sealed record PaymentDto(Guid Id, string ErpReference, decimal Amount, DateOnly PaymentDate, string Status, string? BankReference);

public sealed record AccrualDto(Guid Id, Guid ProjectId, string Period, decimal Amount, string Source, string? Description, bool IsReversed);
public sealed record SaveAccrualRequest(Guid ProjectId, string Period, decimal Amount, string? Description);
public sealed record ForecastDto(Guid Id, Guid ProjectId, DateTime RecordedAtUtc, decimal BudgetAtCompletion, decimal EstimateAtCompletion, decimal Variance,
    decimal VariancePercent, string? Commentary, string? RecordedBy);
public sealed record SaveForecastRequest(decimal EstimateAtCompletion, string Commentary);

public sealed record ErpBatchItem(string ExternalReference, string? InvoiceNumber, string? ProjectNumber, string? PoReference, string? ContractNumber,
    decimal Amount, DateOnly Date, string? Status, string? GlAccount, string? BankReference, string? Description);
public sealed record ErpBatchRequest(Guid BatchId, string MessageType, int ControlCount, decimal ControlTotal, IReadOnlyList<ErpBatchItem> Items);
public sealed record ErpBatchResult(Guid BatchId, string MessageType, int Received, int Processed, int Errors, bool Balanced, IReadOnlyList<string> ErrorMessages);
public sealed record ErpMessageDto(Guid Id, string Direction, string MessageType, string ExternalReference, Guid? BatchId, decimal? ControlAmount, string Status,
    string? Error, int Attempts, DateTime CreatedAtUtc, DateTime? ProcessedAtUtc);
public sealed record ReconciliationDto(Guid Id, Guid BatchId, DateTime RunAtUtc, string MessageType, int ExpectedCount, int ProcessedCount, int ErrorCount,
    decimal ExpectedTotal, decimal ProcessedTotal, bool Balanced, string? Notes);

public sealed record FinanceDashboardDto(FinancialViewDto Portfolio, int InvoicesPendingCertification, decimal PendingCertificationValue,
    int InvoicesValidationFailed, int PotentialDuplicates, int ErpErrors, int UnbalancedBatches, decimal PaidThisYear, decimal CertifiedAwaitingPayment);

public static class ErpMessageTypes
{
    public const string Payment = "Payment";
    public const string Expenditure = "Expenditure";
    public const string Commitment = "Commitment";
    public static readonly IReadOnlyList<string> Inbound = new[] { Payment, Expenditure, Commitment };
}

public interface IFinanceService
{
    Task<FinancialViewDto> ProjectViewAsync(Guid projectId, CancellationToken ct);
    Task<FinancialViewDto> PortfolioViewAsync(string? financialYear, CancellationToken ct);
    Task<IReadOnlyList<CommitmentDto>> ListCommitmentsAsync(Guid projectId, CancellationToken ct);
    Task<CommitmentDto> AddCommitmentAsync(SaveCommitmentRequest request, CancellationToken ct);

    Task<PagedResult<InvoiceDto>> ListInvoicesAsync(Guid? projectId, Guid? contractId, string? status, int page, int pageSize, CancellationToken ct);
    Task<InvoiceDto> GetInvoiceAsync(Guid id, CancellationToken ct);
    Task<InvoiceDto> RegisterInvoiceAsync(RegisterInvoiceRequest request, string? idempotencyKey, CancellationToken ct);
    Task<InvoiceDto> ValidateInvoiceAsync(Guid id, CancellationToken ct);
    Task<WorkflowInstanceDto> SubmitForCertificationAsync(Guid id, CancellationToken ct);
    Task<InvoiceDto> RejectInvoiceAsync(Guid id, string reason, CancellationToken ct);

    Task<IReadOnlyList<AccrualDto>> ListAccrualsAsync(Guid projectId, CancellationToken ct);
    Task<AccrualDto> AddAccrualAsync(SaveAccrualRequest request, CancellationToken ct);
    Task<IReadOnlyList<ForecastDto>> ListForecastsAsync(Guid projectId, CancellationToken ct);
    Task<ForecastDto> RecordForecastAsync(Guid projectId, SaveForecastRequest request, CancellationToken ct);

    Task<ErpBatchResult> ReceiveErpBatchAsync(ErpBatchRequest request, CancellationToken ct);
    Task<ErpBatchResult> RetryErpErrorsAsync(CancellationToken ct);
    Task<IReadOnlyList<ErpMessageDto>> ListErpMessagesAsync(string? status, CancellationToken ct);
    Task<IReadOnlyList<ReconciliationDto>> ListReconciliationsAsync(CancellationToken ct);
    Task<FinanceDashboardDto> DashboardAsync(string? financialYear, CancellationToken ct);
}

/// <summary>Financial control and payment interface (SRS §5.7, §29), BR-006/BR-007. The system never executes payments.</summary>
public sealed class FinanceService : IFinanceService
{
    public const string CertificationWorkflow = "INVOICE_CERTIFICATION";

    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAccessScope _scope;
    private readonly INumberGenerator _numbers;
    private readonly IWorkflowService _workflow;
    private readonly ITransactionLedger _ledger;
    private readonly IAuditWriter _audit;
    private readonly INotifier _notifier;

    public FinanceService(ITetaDbContext db, ICurrentUser user, IClock clock, IAccessScope scope, INumberGenerator numbers, IWorkflowService workflow,
        ITransactionLedger ledger, IAuditWriter audit, INotifier notifier)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _scope = scope;
        _numbers = numbers;
        _workflow = workflow;
        _ledger = ledger;
        _audit = audit;
        _notifier = notifier;
    }

    // =================== Financial views (FR-FIN-001/010) ===================
    public async Task<FinancialViewDto> ProjectViewAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        return await BuildViewAsync(new List<Guid> { projectId }, null, $"Project", projectId, ct);
    }

    public async Task<FinancialViewDto> PortfolioViewAsync(string? financialYear, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var ids = await _db.Projects.AsNoTracking().InScope(scope, p => p.Id).Where(p => p.ProjectNumber != null).Select(p => p.Id).ToListAsync(ct);
        return await BuildViewAsync(ids, financialYear, "Portfolio", null, ct);
    }

    private async Task<FinancialViewDto> BuildViewAsync(List<Guid> projectIds, string? fy, string scopeName, Guid? projectId, CancellationToken ct)
    {
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToListAsync(ct);
        var lines = await _db.BudgetLines.AsNoTracking().Where(b => projectIds.Contains(b.ProjectId) && (fy == null || b.FinancialYear == fy))
            .Select(b => new { b.ProjectId, b.OriginalAmount, b.RevisedAmount, b.ForecastAmount }).ToListAsync(ct);
        var commitments = await _db.Commitments.AsNoTracking().Where(c => projectIds.Contains(c.ProjectId) && !c.IsReleased && (fy == null || c.FinancialYear == fy))
            .Select(c => new { c.ProjectId, c.Amount }).ToListAsync(ct);
        var actuals = await _db.Expenditures.AsNoTracking().Where(e => projectIds.Contains(e.ProjectId) && (fy == null || e.FinancialYear == fy))
            .Select(e => new { e.ProjectId, e.Amount, e.TransactionDate }).ToListAsync(ct);
        var accruals = await _db.Accruals.AsNoTracking().Where(a => projectIds.Contains(a.ProjectId) && !a.IsReversed)
            .Select(a => new { a.ProjectId, a.Amount }).ToListAsync(ct);
        var invoices = await _db.Invoices.AsNoTracking().Where(i => projectIds.Contains(i.ProjectId)
                && i.Status != InvoiceStatus.Rejected && i.Status != InvoiceStatus.Cancelled)
            .Select(i => new { i.ProjectId, i.Amount, i.Status }).ToListAsync(ct);
        var forecasts = (await _db.CostForecasts.AsNoTracking().Where(f => projectIds.Contains(f.ProjectId)).ToListAsync(ct))
            .GroupBy(f => f.ProjectId).Select(g => g.OrderByDescending(f => f.RecordedAtUtc).First()).ToList();

        var rows = projects.Select(p =>
        {
            var budget = lines.Where(l => l.ProjectId == p.Id).Sum(l => l.RevisedAmount);
            if (budget == 0 && fy is null) budget = p.ApprovedBudget;
            var forecastLine = lines.Where(l => l.ProjectId == p.Id).Sum(l => l.ForecastAmount);
            var eac = forecasts.FirstOrDefault(f => f.ProjectId == p.Id)?.EstimateAtCompletion ?? (forecastLine > 0 ? forecastLine : budget);
            return new ProjectFinancialRow(p.Id, p.Reference, p.Name, budget, commitments.Where(c => c.ProjectId == p.Id).Sum(c => c.Amount),
                actuals.Where(a => a.ProjectId == p.Id).Sum(a => a.Amount), eac, budget == 0 ? 0 : Math.Round((eac - budget) / budget * 100m, 1),
                p.Health.ToString());
        }).OrderByDescending(r => r.Budget).ToList();

        var approved = projects.Sum(p => p.ApprovedBudget);
        var revised = rows.Sum(r => r.Budget);
        var committed = rows.Sum(r => r.Committed);
        var actual = rows.Sum(r => r.Actual);
        var eacTotal = rows.Sum(r => r.EstimateAtCompletion);

        // Cash-flow: planned from active contract payment schedules vs actual expenditure by month (last 6 + next 6 months).
        var start = new DateOnly(_clock.Today.Year, _clock.Today.Month, 1).AddMonths(-6);
        var end = start.AddMonths(12);
        var planned = await (from s in _db.PaymentScheduleItems.AsNoTracking()
                             join c in _db.Contracts.AsNoTracking() on s.ContractId equals c.Id
                             where projectIds.Contains(c.ProjectId) && s.PlannedDate >= start && s.PlannedDate < end
                             select new { s.PlannedDate, s.Amount }).ToListAsync(ct);
        var cashflow = Enumerable.Range(0, 12).Select(i =>
        {
            var m = start.AddMonths(i);
            return new CashflowPoint($"{m.Year}-{m.Month:D2}",
                planned.Where(x => x.PlannedDate.Year == m.Year && x.PlannedDate.Month == m.Month).Sum(x => x.Amount),
                actuals.Where(x => x.TransactionDate.Year == m.Year && x.TransactionDate.Month == m.Month).Sum(x => x.Amount));
        }).ToList();

        return new FinancialViewDto(projectId, scopeName, approved, revised, committed, actual, accruals.Sum(a => a.Amount),
            invoices.Sum(i => i.Amount), invoices.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.Amount), eacTotal, eacTotal - revised,
            revised == 0 ? 0 : Math.Round((eacTotal - revised) / revised * 100m, 1), revised - committed, cashflow, rows);
    }

    // =================== Commitments (FR-FIN-003, FR-CON-009) ===================
    public async Task<IReadOnlyList<CommitmentDto>> ListCommitmentsAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        return await _db.Commitments.AsNoTracking().Where(c => c.ProjectId == projectId).OrderByDescending(c => c.CommitmentDate)
            .Select(c => new CommitmentDto(c.Id, c.ProjectId, c.ContractId, c.PoReference, c.FinancialYear, c.Amount, c.CommitmentDate, c.Source.ToString(),
                c.IsReleased, c.ErpReference)).ToListAsync(ct);
    }

    public async Task<CommitmentDto> AddCommitmentAsync(SaveCommitmentRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("projectId", r.ProjectId).Required("poReference", r.PoReference, 50).Positive("amount", r.Amount).ThrowIfInvalid();
        await _scope.EnsureProjectAsync(r.ProjectId, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == r.ProjectId, ct);
        project.EnsureActive();
        if (r.ContractId is { } cid)
        {
            var contract = await _db.Contracts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cid && c.ProjectId == r.ProjectId, ct)
                           ?? throw new ValidationException("contractId", "The contract does not belong to this project.");
            var existing = (await _db.Commitments.Where(c => c.ContractId == cid && !c.IsReleased).Select(c => c.Amount).ToListAsync(ct)).Sum();
            if (!contract.CanCommit(existing, r.Amount))
                throw new DomainException($"Commitments would exceed the approved contract ceiling of R {contract.RevisedValue:N2}. An approved variation is required.", "FR-CON-009");
        }
        var budget = (await _db.BudgetLines.Where(b => b.ProjectId == r.ProjectId).Select(b => b.RevisedAmount).ToListAsync(ct)).Sum();
        var committed = (await _db.Commitments.Where(c => c.ProjectId == r.ProjectId && !c.IsReleased).Select(c => c.Amount).ToListAsync(ct)).Sum();
        if (committed + r.Amount > budget)
            throw new DomainException($"The commitment exceeds the available project budget (R {budget - committed:N2} available).", "FR-BUD-005");

        var c = new Commitment
        {
            ProjectId = r.ProjectId, ContractId = r.ContractId, PoReference = r.PoReference, FinancialYear = Fy.For(r.CommitmentDate), Amount = r.Amount,
            CommitmentDate = r.CommitmentDate, Source = r.ContractId is null ? CommitmentSource.PurchaseOrder : CommitmentSource.Contract, ErpReference = r.ErpReference
        };
        _db.Commitments.Add(c);
        await _db.SaveChangesAsync(ct);
        return new CommitmentDto(c.Id, c.ProjectId, c.ContractId, c.PoReference, c.FinancialYear, c.Amount, c.CommitmentDate, c.Source.ToString(), c.IsReleased, c.ErpReference);
    }

    // =================== Invoices (FR-FIN-004/005/006, BR-006) ===================
    public async Task<PagedResult<InvoiceDto>> ListInvoicesAsync(Guid? projectId, Guid? contractId, string? status, int page, int pageSize, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.Invoices.AsNoTracking().InScope(scope, i => i.ProjectId);
        if (projectId is { } pid) q = q.Where(i => i.ProjectId == pid);
        if (contractId is { } cid) q = q.Where(i => i.ContractId == cid);
        if (Enum.TryParse<InvoiceStatus>(status, true, out var st)) q = q.Where(i => i.Status == st);
        var total = await q.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var rows = await q.OrderByDescending(i => i.ReceivedDate).ThenByDescending(i => i.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<InvoiceDto>(await ToDtosAsync(rows, ct), total, page, pageSize);
    }

    public async Task<InvoiceDto> GetInvoiceAsync(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Invoice", id);
        if (_user.IsInRole(Roles.Supplier) && !_user.HasPermission(Permissions.FinanceRead))
        {
            // Supplier-portal users see only their own organisation's invoices.
            if (!await _db.SupplierUsers.AnyAsync(s => s.UserId == _user.UserId && s.SupplierId == invoice.SupplierId, ct))
                throw new NotFoundException("Invoice", id);
        }
        else
        {
            await _scope.EnsureProjectAsync(invoice.ProjectId, ct);
        }
        return (await ToDtosAsync(new[] { invoice }, ct))[0];
    }

    public async Task<InvoiceDto> RegisterInvoiceAsync(RegisterInvoiceRequest r, string? idempotencyKey, CancellationToken ct)
    {
        new Validator().RequiredId("contractId", r.ContractId).Required("supplierInvoiceNumber", r.SupplierInvoiceNumber, 50)
            .Positive("amount", r.Amount).NonNegative("vatAmount", r.VatAmount).Optional("poReference", r.PoReference, 50).ThrowIfInvalid();

        // Idempotency (SRS §8.1): a retried request with the same key returns the original invoice.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.Invoices.AsNoTracking().SingleOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, ct);
            if (existing is not null) return await GetInvoiceAsync(existing.Id, ct);
        }

        var contract = await _db.Contracts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == r.ContractId, ct) ?? throw new ValidationException("contractId", "Contract not found.");
        await EnsureInvoiceAccessAsync(contract, ct);
        if (r.DeliverableId is { } did && !await _db.Deliverables.AnyAsync(d => d.Id == did && d.ContractId == r.ContractId, ct))
            throw new ValidationException("deliverableId", "The deliverable does not belong to this contract.");

        var invoice = new Invoice
        {
            Number = await _numbers.NextAsync(NumberPrefixes.Invoice, ct), SupplierInvoiceNumber = r.SupplierInvoiceNumber.Trim(), ContractId = contract.Id,
            DeliverableId = r.DeliverableId, SupplierId = contract.SupplierId, ProjectId = contract.ProjectId, PoReference = r.PoReference,
            InvoiceDate = r.InvoiceDate, ReceivedDate = r.ReceivedDate ?? _clock.Today, Amount = r.Amount, VatAmount = r.VatAmount,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey
        };
        _db.Invoices.Add(invoice);
        _ledger.Record(nameof(Invoice), invoice.Id, "RegisterInvoice", invoice.ProjectId);

        // Carry the deliverable acceptor into the invoice's SoD ledger so they cannot also certify payment.
        if (r.DeliverableId is { } deliverableId)
        {
            var acceptor = await _db.Deliverables.Where(d => d.Id == deliverableId).Select(d => new { d.AcceptedByUserId, d.AcceptedBy }).SingleAsync(ct);
            if (acceptor.AcceptedByUserId is not null)
            {
                _db.TransactionActions.Add(new Domain.Workflow.TransactionAction
                {
                    EntityType = nameof(Invoice), EntityId = invoice.Id, ActionCode = "AcceptDeliverable", UserId = acceptor.AcceptedByUserId,
                    Username = acceptor.AcceptedBy, OccurredAtUtc = _clock.UtcNow, ProjectId = invoice.ProjectId
                });
            }
        }

        await ApplyValidationAsync(invoice, ct);
        await _db.SaveChangesAsync(ct);
        return await GetInvoiceAsync(invoice.Id, ct);
    }

    public async Task<InvoiceDto> ValidateInvoiceAsync(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Invoice", id);
        await _scope.EnsureProjectAsync(invoice.ProjectId, ct);
        if (invoice.Status is not (InvoiceStatus.Registered or InvoiceStatus.ValidationFailed))
            throw new DomainException($"Invoice is {invoice.Status}; only unvalidated invoices are re-validated.", "FR-FIN-005");
        await ApplyValidationAsync(invoice, ct);
        await _db.SaveChangesAsync(ct);
        return await GetInvoiceAsync(id, ct);
    }

    /// <summary>Three-way control (FR-FIN-005, BR-006, SRS §29) and duplicate detection (FR-FIN-004).</summary>
    internal async Task<IReadOnlyList<string>> ThreeWayCheckAsync(Invoice invoice, CancellationToken ct)
    {
        var messages = new List<string>();
        var contract = await _db.Contracts.AsNoTracking().SingleAsync(c => c.Id == invoice.ContractId, ct);
        if (contract.Status != ContractStatus.Active) messages.Add($"Contract {contract.ContractNumber} is not active ({contract.Status}).");
        if (!string.IsNullOrWhiteSpace(contract.PoReference) && !string.Equals(contract.PoReference, invoice.PoReference, StringComparison.OrdinalIgnoreCase))
            messages.Add($"PO reference '{invoice.PoReference}' does not match the contract PO '{contract.PoReference}'.");
        if (string.IsNullOrWhiteSpace(invoice.PoReference) && string.IsNullOrWhiteSpace(contract.PoReference))
            messages.Add("No valid contract/PO reference is recorded.");

        if (invoice.DeliverableId is not { } did)
        {
            messages.Add("The invoice is not linked to a deliverable.");
        }
        else
        {
            var deliverable = await _db.Deliverables.AsNoTracking().SingleAsync(d => d.Id == did, ct);
            if (deliverable.AcceptanceStatus != AcceptanceStatus.Accepted) messages.Add($"Deliverable {deliverable.Number} has not been accepted.");
            var invoicedForDeliverable = (await _db.Invoices.Where(i => i.DeliverableId == did && i.Id != invoice.Id
                    && i.Status != InvoiceStatus.Rejected && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.ValidationFailed)
                .Select(i => i.Amount).ToListAsync(ct)).Sum();
            if (invoicedForDeliverable + invoice.Amount > deliverable.PayableAmount)
                messages.Add($"Invoice exceeds the accepted payable amount (R {deliverable.PayableAmount - invoicedForDeliverable:N2} remaining on {deliverable.Number}).");
        }

        var invoicedOnContract = (await _db.Invoices.Where(i => i.ContractId == contract.Id && i.Id != invoice.Id
                && i.Status != InvoiceStatus.Rejected && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.ValidationFailed)
            .Select(i => i.Amount).ToListAsync(ct)).Sum();
        if (invoicedOnContract + invoice.Amount > contract.RevisedValue)
            messages.Add($"Cumulative invoicing would exceed the contract ceiling of R {contract.RevisedValue:N2}.");

        var duplicate = await _db.Invoices.AnyAsync(i => i.Id != invoice.Id && i.SupplierId == invoice.SupplierId && i.Status != InvoiceStatus.Rejected
            && i.Status != InvoiceStatus.Cancelled
            && (i.SupplierInvoiceNumber == invoice.SupplierInvoiceNumber || (i.Amount == invoice.Amount && i.InvoiceDate == invoice.InvoiceDate)), ct);
        invoice.PotentialDuplicate = duplicate;
        if (duplicate) messages.Add("Potential duplicate invoice (same supplier invoice number, or same amount and date).");
        return messages;
    }

    private async Task ApplyValidationAsync(Invoice invoice, CancellationToken ct)
    {
        var messages = await ThreeWayCheckAsync(invoice, ct);
        invoice.ValidationMessages = messages.Count == 0 ? null : string.Join("\n", messages);
        invoice.Status = messages.Count == 0 ? InvoiceStatus.PendingCertification : InvoiceStatus.ValidationFailed;
    }

    public async Task<WorkflowInstanceDto> SubmitForCertificationAsync(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Invoice", id);
        await _scope.EnsureProjectAsync(invoice.ProjectId, ct);
        if (invoice.Status != InvoiceStatus.PendingCertification)
            throw new DomainException("Only a validated invoice can be routed for certification.", "BR-006");
        var messages = await ThreeWayCheckAsync(invoice, ct);
        if (messages.Count > 0)
        {
            invoice.Status = InvoiceStatus.ValidationFailed;
            invoice.ValidationMessages = string.Join("\n", messages);
            await _db.SaveChangesAsync(ct);
            throw new DomainException("Three-way validation failed: " + string.Join(" ", messages), "BR-006");
        }
        var contract = await _db.Contracts.AsNoTracking().SingleAsync(c => c.Id == invoice.ContractId, ct);
        var instance = await _workflow.StartAsync(new StartWorkflowRequest(CertificationWorkflow, nameof(Invoice), invoice.Id, invoice.Number,
            $"Certify {invoice.SupplierInvoiceNumber} on {contract.ContractNumber}", invoice.Amount, invoice.ProjectId, $"finance/invoices/{invoice.Id}"), ct);
        invoice.WorkflowInstanceId = instance.Id;
        await _db.SaveChangesAsync(ct);
        return await _workflow.GetAsync(instance.Id, ct);
    }

    public async Task<InvoiceDto> RejectInvoiceAsync(Guid id, string reason, CancellationToken ct)
    {
        new Validator().Required("reason", reason, 2000).ThrowIfInvalid();
        var invoice = await _db.Invoices.SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Invoice", id);
        await _scope.EnsureProjectAsync(invoice.ProjectId, ct);
        if (invoice.Status is InvoiceStatus.Certified or InvoiceStatus.SubmittedToErp or InvoiceStatus.Paid)
            throw new DomainException("A certified invoice cannot be rejected here.", "FR-FIN-006");
        await _workflow.CancelForEntityAsync(nameof(Invoice), id, reason, ct);
        invoice.Status = InvoiceStatus.Rejected;
        invoice.RejectionReason = reason;
        await _db.SaveChangesAsync(ct);
        return await GetInvoiceAsync(id, ct);
    }

    // =================== Accruals & forecasts (FR-FIN-008/009) ===================
    public async Task<IReadOnlyList<AccrualDto>> ListAccrualsAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        return await _db.Accruals.AsNoTracking().Where(a => a.ProjectId == projectId).OrderByDescending(a => a.Period)
            .Select(a => new AccrualDto(a.Id, a.ProjectId, a.Period, a.Amount, a.Source, a.Description, a.IsReversed)).ToListAsync(ct);
    }

    public async Task<AccrualDto> AddAccrualAsync(SaveAccrualRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("projectId", r.ProjectId).Required("period", r.Period, 20).Positive("amount", r.Amount).ThrowIfInvalid();
        await _scope.EnsureProjectAsync(r.ProjectId, ct);
        var a = new Accrual { ProjectId = r.ProjectId, Period = r.Period, Amount = r.Amount, Description = r.Description, Source = "Manual" };
        _db.Accruals.Add(a);
        await _db.SaveChangesAsync(ct);
        return new AccrualDto(a.Id, a.ProjectId, a.Period, a.Amount, a.Source, a.Description, a.IsReversed);
    }

    public async Task<IReadOnlyList<ForecastDto>> ListForecastsAsync(Guid projectId, CancellationToken ct)
    {
        await _scope.EnsureProjectAsync(projectId, ct);
        return await _db.CostForecasts.AsNoTracking().Where(f => f.ProjectId == projectId).OrderByDescending(f => f.RecordedAtUtc)
            .Select(f => new ForecastDto(f.Id, f.ProjectId, f.RecordedAtUtc, f.BudgetAtCompletion, f.EstimateAtCompletion, f.Variance, f.VariancePercent,
                f.Commentary, f.RecordedBy)).ToListAsync(ct);
    }

    public async Task<ForecastDto> RecordForecastAsync(Guid projectId, SaveForecastRequest r, CancellationToken ct)
    {
        new Validator().NonNegative("estimateAtCompletion", r.EstimateAtCompletion).Required("commentary", r.Commentary, 4000).ThrowIfInvalid();
        await _scope.EnsureProjectAsync(projectId, ct);
        var project = await _db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        var bac = project.ApprovedBudget;
        var f = new CostForecast
        {
            ProjectId = projectId, RecordedAtUtc = _clock.UtcNow, BudgetAtCompletion = bac, EstimateAtCompletion = r.EstimateAtCompletion,
            Variance = r.EstimateAtCompletion - bac, VariancePercent = bac == 0 ? 0 : Math.Round((r.EstimateAtCompletion - bac) / bac * 100m, 2),
            Commentary = r.Commentary, RecordedBy = _user.DisplayName ?? _user.Username
        };
        _db.CostForecasts.Add(f);
        await _db.SaveChangesAsync(ct);
        return new ForecastDto(f.Id, f.ProjectId, f.RecordedAtUtc, f.BudgetAtCompletion, f.EstimateAtCompletion, f.Variance, f.VariancePercent, f.Commentary, f.RecordedBy);
    }

    // =================== ERP interface (FR-FIN-002/007, SRS §7.1) ===================
    public async Task<ErpBatchResult> ReceiveErpBatchAsync(ErpBatchRequest r, CancellationToken ct)
    {
        new Validator().RequiredId("batchId", r.BatchId).OneOf("messageType", r.MessageType, ErpMessageTypes.Inbound)
            .Must(r.Items is { Count: > 0 }, "items", "The batch contains no items.").ThrowIfInvalid();
        if (await _db.ErpReconciliations.AnyAsync(x => x.BatchId == r.BatchId, ct))
        {
            // Idempotent: re-sending a batch returns its original reconciliation.
            var prior = await _db.ErpReconciliations.AsNoTracking().FirstAsync(x => x.BatchId == r.BatchId, ct);
            return new ErpBatchResult(r.BatchId, prior.MessageType, prior.ExpectedCount, prior.ProcessedCount, prior.ErrorCount, prior.Balanced,
                new[] { "Batch already received; returning the original result." });
        }

        var messages = new List<ErpInterfaceMessage>();
        foreach (var item in r.Items)
        {
            var message = new ErpInterfaceMessage
            {
                Direction = InterfaceDirection.Inbound, MessageType = r.MessageType, ExternalReference = item.ExternalReference, BatchId = r.BatchId,
                PayloadJson = JsonSerializer.Serialize(item), ControlAmount = item.Amount, Status = InterfaceStatus.Received
            };
            _db.ErpInterfaceMessages.Add(message);
            messages.Add(message);
        }
        await _db.SaveChangesAsync(ct);

        var errors = new List<string>();
        foreach (var message in messages)
        {
            var error = await ProcessMessageAsync(message, ct);
            if (error is not null) errors.Add($"{message.ExternalReference}: {error}");
        }

        var processed = messages.Where(m => m.Status == InterfaceStatus.Processed).ToList();
        var balanced = r.ControlCount == messages.Count && r.ControlTotal == messages.Sum(m => m.ControlAmount ?? 0) && errors.Count == 0;
        _db.ErpReconciliations.Add(new ErpReconciliation
        {
            BatchId = r.BatchId, RunAtUtc = _clock.UtcNow, MessageType = r.MessageType, ExpectedCount = r.ControlCount, ProcessedCount = processed.Count,
            ErrorCount = errors.Count, ExpectedTotal = r.ControlTotal, ProcessedTotal = processed.Sum(m => m.ControlAmount ?? 0), Balanced = balanced,
            Notes = balanced ? null : $"Control count {r.ControlCount} vs received {messages.Count}; control total {r.ControlTotal:N2} vs received {messages.Sum(m => m.ControlAmount ?? 0):N2}; {errors.Count} error(s)."
        });
        if (!balanced)
        {
            await _notifier.NotifyRoleAsync(Roles.FinanceOfficer, NotificationTemplates.ErpReconciliationIssue,
                new Dictionary<string, string?> { ["MessageType"] = r.MessageType, ["Details"] = $"{errors.Count} error(s), batch {r.BatchId}" },
                "finance/erp", "ErpBatch", r.BatchId, null, ct);
        }
        _audit.Write("Finance", "ErpBatch", r.BatchId.ToString(), "ErpBatchReceived",
            new { r.MessageType, r.ControlCount, r.ControlTotal, Processed = processed.Count, Errors = errors.Count, Balanced = balanced });
        await _db.SaveChangesAsync(ct);
        return new ErpBatchResult(r.BatchId, r.MessageType, messages.Count, processed.Count, errors.Count, balanced, errors);
    }

    public async Task<ErpBatchResult> RetryErpErrorsAsync(CancellationToken ct)
    {
        var queued = await _db.ErpInterfaceMessages.Where(m => m.Direction == InterfaceDirection.Inbound && m.Status == InterfaceStatus.Error && m.Attempts < 10)
            .OrderBy(m => m.CreatedAtUtc).Take(500).ToListAsync(ct);
        var errors = new List<string>();
        foreach (var m in queued)
        {
            var error = await ProcessMessageAsync(m, ct);
            if (error is not null) errors.Add($"{m.ExternalReference}: {error}");
        }
        await _db.SaveChangesAsync(ct);
        return new ErpBatchResult(Guid.Empty, "Retry", queued.Count, queued.Count - errors.Count, errors.Count, errors.Count == 0, errors);
    }

    private async Task<string?> ProcessMessageAsync(ErpInterfaceMessage message, CancellationToken ct)
    {
        message.Attempts++;
        try
        {
            var item = JsonSerializer.Deserialize<ErpBatchItem>(message.PayloadJson) ?? throw new InvalidOperationException("Unreadable payload.");
            switch (message.MessageType)
            {
                case ErpMessageTypes.Payment:
                    var invoice = await _db.Invoices.SingleOrDefaultAsync(i => i.Number == item.InvoiceNumber, ct)
                                  ?? throw new InvalidOperationException($"Invoice {item.InvoiceNumber} not found.");
                    if (invoice.Status is not (InvoiceStatus.Certified or InvoiceStatus.SubmittedToErp or InvoiceStatus.Paid))
                        throw new InvalidOperationException($"Invoice {invoice.Number} is {invoice.Status}; only certified invoices can be paid.");
                    if (await _db.Payments.AnyAsync(p => p.ErpReference == item.ExternalReference, ct)) break;   // idempotent consumer
                    var status = Enum.TryParse<PaymentStatus>(item.Status, true, out var ps) ? ps : PaymentStatus.Paid;
                    _db.Payments.Add(new Payment
                    {
                        InvoiceId = invoice.Id, ProjectId = invoice.ProjectId, ErpReference = item.ExternalReference, Amount = item.Amount,
                        PaymentDate = item.Date, Status = status, BankReference = item.BankReference
                    });
                    if (status == PaymentStatus.Paid)
                    {
                        invoice.Status = InvoiceStatus.Paid;
                        if (!await _db.Expenditures.AnyAsync(e => e.ErpReference == item.ExternalReference, ct))
                        {
                            _db.Expenditures.Add(new Expenditure
                            {
                                ProjectId = invoice.ProjectId, InvoiceId = invoice.Id, FinancialYear = Fy.For(item.Date), Amount = item.Amount,
                                TransactionDate = item.Date, GlAccount = item.GlAccount, ErpReference = item.ExternalReference, Description = $"Payment of {invoice.Number}"
                            });
                        }
                    }
                    break;

                case ErpMessageTypes.Expenditure:
                    var project = await _db.Projects.SingleOrDefaultAsync(p => p.ProjectNumber == item.ProjectNumber, ct)
                                  ?? throw new InvalidOperationException($"Project {item.ProjectNumber} not found.");
                    if (await _db.Expenditures.AnyAsync(e => e.ErpReference == item.ExternalReference, ct)) break;
                    _db.Expenditures.Add(new Expenditure
                    {
                        ProjectId = project.Id, FinancialYear = Fy.For(item.Date), Amount = item.Amount, TransactionDate = item.Date, GlAccount = item.GlAccount,
                        ErpReference = item.ExternalReference, Description = item.Description
                    });
                    break;

                case ErpMessageTypes.Commitment:
                    var p2 = await _db.Projects.SingleOrDefaultAsync(p => p.ProjectNumber == item.ProjectNumber, ct)
                             ?? throw new InvalidOperationException($"Project {item.ProjectNumber} not found.");
                    if (await _db.Commitments.AnyAsync(c => c.ErpReference == item.ExternalReference, ct)) break;
                    Guid? contractId = null;
                    if (!string.IsNullOrWhiteSpace(item.ContractNumber))
                    {
                        contractId = await _db.Contracts.Where(c => c.ContractNumber == item.ContractNumber).Select(c => (Guid?)c.Id).SingleOrDefaultAsync(ct)
                                     ?? throw new InvalidOperationException($"Contract {item.ContractNumber} not found.");
                    }
                    _db.Commitments.Add(new Commitment
                    {
                        ProjectId = p2.Id, ContractId = contractId, PoReference = item.PoReference, FinancialYear = Fy.For(item.Date), Amount = item.Amount,
                        CommitmentDate = item.Date, Source = CommitmentSource.PurchaseOrder, ErpReference = item.ExternalReference
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported message type {message.MessageType}.");
            }
            message.Status = InterfaceStatus.Processed;
            message.ProcessedAtUtc = _clock.UtcNow;
            message.Error = null;
            return null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException)
        {
            message.Status = InterfaceStatus.Error;
            message.Error = ex.Message;
            return ex.Message;
        }
    }

    public async Task<IReadOnlyList<ErpMessageDto>> ListErpMessagesAsync(string? status, CancellationToken ct)
    {
        var q = _db.ErpInterfaceMessages.AsNoTracking();
        if (Enum.TryParse<InterfaceStatus>(status, true, out var st)) q = q.Where(m => m.Status == st);
        return await q.OrderByDescending(m => m.CreatedAtUtc).Take(500)
            .Select(m => new ErpMessageDto(m.Id, m.Direction.ToString(), m.MessageType, m.ExternalReference, m.BatchId, m.ControlAmount, m.Status.ToString(),
                m.Error, m.Attempts, m.CreatedAtUtc, m.ProcessedAtUtc)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReconciliationDto>> ListReconciliationsAsync(CancellationToken ct) =>
        await _db.ErpReconciliations.AsNoTracking().OrderByDescending(r => r.RunAtUtc).Take(200)
            .Select(r => new ReconciliationDto(r.Id, r.BatchId, r.RunAtUtc, r.MessageType, r.ExpectedCount, r.ProcessedCount, r.ErrorCount, r.ExpectedTotal,
                r.ProcessedTotal, r.Balanced, r.Notes)).ToListAsync(ct);

    public async Task<FinanceDashboardDto> DashboardAsync(string? financialYear, CancellationToken ct)
    {
        var portfolio = await PortfolioViewAsync(financialYear, ct);
        var scope = await _scope.GetAsync(ct);
        var invoices = await _db.Invoices.AsNoTracking().InScope(scope, i => i.ProjectId)
            .Select(i => new { i.Status, i.Amount, i.PotentialDuplicate }).ToListAsync(ct);
        var fy = financialYear ?? Fy.For(_clock.Today);
        var paid = (await _db.Payments.AsNoTracking().InScope(scope, p => p.ProjectId).Where(p => p.Status == PaymentStatus.Paid)
            .Select(p => new { p.Amount, p.PaymentDate }).ToListAsync(ct)).Where(p => Fy.For(p.PaymentDate) == fy).Sum(p => p.Amount);
        var erpErrors = await _db.ErpInterfaceMessages.CountAsync(m => m.Status == InterfaceStatus.Error, ct);
        var unbalanced = await _db.ErpReconciliations.CountAsync(r => !r.Balanced, ct);
        return new FinanceDashboardDto(portfolio,
            invoices.Count(i => i.Status == InvoiceStatus.PendingCertification), invoices.Where(i => i.Status == InvoiceStatus.PendingCertification).Sum(i => i.Amount),
            invoices.Count(i => i.Status == InvoiceStatus.ValidationFailed), invoices.Count(i => i.PotentialDuplicate && i.Status != InvoiceStatus.Rejected),
            erpErrors, unbalanced, paid,
            invoices.Where(i => i.Status is InvoiceStatus.Certified or InvoiceStatus.SubmittedToErp).Sum(i => i.Amount));
    }

    // ----- helpers -----
    private async Task EnsureInvoiceAccessAsync(Contract contract, CancellationToken ct)
    {
        // Supplier-portal users register invoices only on their own contracts.
        if (_user.IsInRole(Roles.Supplier) && !_user.HasPermission(Permissions.FinanceManage))
        {
            var mine = await _db.SupplierUsers.AnyAsync(s => s.UserId == _user.UserId && s.SupplierId == contract.SupplierId, ct);
            if (!mine) throw new NotFoundException("Contract", contract.Id);
            return;
        }
        await _scope.EnsureProjectAsync(contract.ProjectId, ct);
    }

    private async Task<List<InvoiceDto>> ToDtosAsync(IReadOnlyCollection<Invoice> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var contractIds = rows.Select(r => r.ContractId).Distinct().ToList();
        var contracts = await _db.Contracts.AsNoTracking().Where(c => contractIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.ContractNumber, ct);
        var supplierIds = rows.Select(r => r.SupplierId).Distinct().ToList();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.LegalName, ct);
        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();
        var projects = await _db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, Ref = p.ProjectNumber ?? p.DraftReference }).ToDictionaryAsync(p => p.Id, p => p.Ref, ct);
        var deliverableIds = rows.Where(r => r.DeliverableId != null).Select(r => r.DeliverableId!.Value).ToList();
        var deliverables = await _db.Deliverables.AsNoTracking().Where(d => deliverableIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var payments = await _db.Payments.AsNoTracking().Where(p => ids.Contains(p.InvoiceId)).ToListAsync(ct);
        return rows.Select(i => new InvoiceDto(i.Id, i.Number, i.SupplierInvoiceNumber, i.ContractId, contracts.GetValueOrDefault(i.ContractId, "?"),
            i.DeliverableId, i.DeliverableId is { } did ? deliverables.GetValueOrDefault(did) : null, i.SupplierId, suppliers.GetValueOrDefault(i.SupplierId, "?"),
            i.ProjectId, projects.GetValueOrDefault(i.ProjectId, "?"), i.PoReference, i.InvoiceDate, i.ReceivedDate, i.Amount, i.VatAmount, i.Status.ToString(),
            i.PotentialDuplicate, (i.ValidationMessages ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries), i.CertifiedAtUtc, i.CertifiedBy,
            i.RejectionReason,
            payments.Where(p => p.InvoiceId == i.Id).Select(p => new PaymentDto(p.Id, p.ErpReference, p.Amount, p.PaymentDate, p.Status.ToString(), p.BankReference)).ToList(),
            i.WorkflowInstanceId, i.Version)).ToList();
    }
}

/// <summary>
/// Certification workflow completes (FR-FIN-006, SRS §29). Re-runs the three-way check at the moment
/// of certification and hands the certified invoice to the ERP for payment.
/// </summary>
public sealed class InvoiceCertificationHandler : IWorkflowCompletionHandler
{
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;
    private readonly IErpGateway _erp;

    public InvoiceCertificationHandler(ITetaDbContext db, IClock clock, IErpGateway erp)
    {
        _db = db;
        _clock = clock;
        _erp = erp;
    }

    public string EntityType => nameof(Invoice);

    public async Task OnCompletedAsync(WorkflowInstance instance, WorkflowState outcome, string? comment, CancellationToken ct)
    {
        var invoice = await _db.Invoices.SingleAsync(i => i.Id == instance.EntityId, ct);
        if (outcome != WorkflowState.Approved)
        {
            invoice.Status = outcome == WorkflowState.Rejected ? InvoiceStatus.Rejected : InvoiceStatus.Registered;
            invoice.RejectionReason = comment;
            return;
        }

        // Final guard: contract active, deliverable accepted, amount within payable (BR-006).
        var contract = await _db.Contracts.AsNoTracking().SingleAsync(c => c.Id == invoice.ContractId, ct);
        var deliverable = invoice.DeliverableId is { } did ? await _db.Deliverables.AsNoTracking().SingleOrDefaultAsync(d => d.Id == did, ct) : null;
        if (contract.Status != ContractStatus.Active || deliverable?.AcceptanceStatus != AcceptanceStatus.Accepted || invoice.PotentialDuplicate)
            throw new DomainException("Certification blocked: contract must be active, deliverable accepted and the invoice not a potential duplicate.", "BR-006");

        var last = instance.Tasks.Where(t => t.Decision == TaskDecision.Approved).OrderByDescending(t => t.DecidedAtUtc).FirstOrDefault();
        invoice.Status = InvoiceStatus.Certified;
        invoice.CertifiedAtUtc = _clock.UtcNow;
        invoice.CertifiedBy = last?.DecidedBy;
        invoice.CertifiedByUserId = last?.DecidedByUserId;

        var payload = JsonSerializer.Serialize(new
        {
            invoice.Number, invoice.SupplierInvoiceNumber, invoice.Amount, invoice.VatAmount, invoice.PoReference, contract.ContractNumber,
            CertifiedAtUtc = invoice.CertifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture), invoice.CertifiedBy
        });
        await _erp.SubmitCertifiedInvoiceAsync(invoice.Id, payload, ct);
        invoice.Status = InvoiceStatus.SubmittedToErp;
    }
}
