using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Finance;

public enum InvoiceStatus
{
    Registered,
    ValidationFailed,
    PendingCertification,
    Certified,
    Rejected,
    SubmittedToErp,
    Paid,
    Cancelled
}

/// <summary>
/// Supplier invoice metadata linked to contract/PO/deliverable (FR-FIN-004). Certification requires
/// the three-way match (FR-FIN-005, BR-006) and delegated authority (FR-FIN-006, BR-007).
/// Payment itself is executed by the ERP; this system never pays (SRS §2.3).
/// </summary>
public class Invoice : AuditableEntity
{
    public string Number { get; set; } = default!;
    public string SupplierInvoiceNumber { get; set; } = default!;
    public Guid ContractId { get; set; }
    public Guid? DeliverableId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid ProjectId { get; set; }
    public string? PoReference { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly ReceivedDate { get; set; }
    public decimal Amount { get; set; }
    public decimal VatAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Registered;
    public bool PotentialDuplicate { get; set; }
    public string? ValidationMessages { get; set; }
    public DateTime? CertifiedAtUtc { get; set; }
    public string? CertifiedBy { get; set; }
    public Guid? CertifiedByUserId { get; set; }
    public string? RejectionReason { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
}

public enum PaymentStatus
{
    Scheduled,
    Paid,
    Failed,
    Reversed
}

/// <summary>Payment status/reference received from the finance platform; read-only to project users (FR-FIN-007).</summary>
public class Payment : AuditableEntity
{
    public Guid InvoiceId { get; set; }
    public Guid ProjectId { get; set; }
    public string ErpReference { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentStatus Status { get; set; }
    public string? BankReference { get; set; }
}

public enum CommitmentSource
{
    Contract,
    PurchaseOrder,
    Other
}

/// <summary>Procurement/contract commitment against a project budget (FR-FIN-003).</summary>
public class Commitment : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ContractId { get; set; }
    public string? PoReference { get; set; }
    public string FinancialYear { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateOnly CommitmentDate { get; set; }
    public CommitmentSource Source { get; set; }
    public bool IsReleased { get; set; }
    public string? ErpReference { get; set; }
}

/// <summary>Actual expenditure posted in the ERP (FR-FIN-001).</summary>
public class Expenditure : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string FinancialYear { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? GlAccount { get; set; }
    public string ErpReference { get; set; } = default!;
    public string? Description { get; set; }
}

/// <summary>Accrual estimate at a reporting cut-off (FR-FIN-008).</summary>
public class Accrual : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public string Period { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Source { get; set; } = "Manual";
    public string? Description { get; set; }
    public bool IsReversed { get; set; }
}

/// <summary>Estimate-at-completion history (FR-FIN-009).</summary>
public class CostForecast : Entity
{
    public Guid ProjectId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public decimal BudgetAtCompletion { get; set; }
    public decimal EstimateAtCompletion { get; set; }
    public decimal Variance { get; set; }
    public decimal VariancePercent { get; set; }
    public string? Commentary { get; set; }
    public string? RecordedBy { get; set; }
}

public enum InterfaceDirection
{
    Inbound,
    Outbound
}

public enum InterfaceStatus
{
    Pending,
    Received,
    Processed,
    Sent,
    Error,
    Reconciled
}

/// <summary>ERP interface message with error queue and retry tracking (FR-FIN-002, SRS §7.1).</summary>
public class ErpInterfaceMessage : AuditableEntity
{
    public InterfaceDirection Direction { get; set; }
    public string MessageType { get; set; } = default!;
    public string ExternalReference { get; set; } = default!;
    public Guid? BatchId { get; set; }
    public string PayloadJson { get; set; } = default!;
    public decimal? ControlAmount { get; set; }
    public InterfaceStatus Status { get; set; } = InterfaceStatus.Received;
    public string? Error { get; set; }
    public int Attempts { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}

/// <summary>Control totals / reconciliation for an interface batch (FR-FIN-002, RPT-015).</summary>
public class ErpReconciliation : AuditableEntity
{
    public Guid BatchId { get; set; }
    public DateTime RunAtUtc { get; set; }
    public string MessageType { get; set; } = default!;
    public int ExpectedCount { get; set; }
    public int ProcessedCount { get; set; }
    public int ErrorCount { get; set; }
    public decimal ExpectedTotal { get; set; }
    public decimal ProcessedTotal { get; set; }
    public bool Balanced { get; set; }
    public string? Notes { get; set; }
}
