using Platform.Core;
using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Budget;

/// <summary>
/// Approved project budget line by financial year, cost category and funding source (FR-BUD-001).
/// The original amount is the immutable baseline once baselined; revisions change RevisedAmount and
/// are recorded in <see cref="BudgetRevision"/> (FR-BUD-002, BR-005). Carry-over lines are flagged (FR-BUD-010).
/// </summary>
public class ProjectBudgetLine : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public string FinancialYear { get; set; } = default!;
    public string CostCategory { get; set; } = default!;
    public string FundingSource { get; set; } = default!;
    public decimal OriginalAmount { get; set; }
    public decimal RevisedAmount { get; set; }
    public decimal ForecastAmount { get; set; }
    public bool IsCarryOver { get; set; }
    public string? CarryOverFromYear { get; set; }
    public bool IsBaselined { get; set; }

    public void SetOriginal(decimal amount)
    {
        if (IsBaselined)
            throw new DomainException("The original budget baseline is immutable. Use a budget revision.", "FR-BUD-002");
        if (amount < 0) throw new DomainException("Budget amounts cannot be negative.", "FR-BUD-001");
        OriginalAmount = amount;
        RevisedAmount = amount;
        ForecastAmount = amount;
    }
}

/// <summary>History of approved budget revisions (FR-BUD-002).</summary>
public class BudgetRevision : Entity
{
    public Guid ProjectId { get; set; }
    public Guid BudgetLineId { get; set; }
    public decimal PreviousRevised { get; set; }
    public decimal NewRevised { get; set; }
    public string Reason { get; set; } = default!;
    public Guid? ChangeRequestId { get; set; }
    public DateTime RevisedAtUtc { get; set; }
    public string? RevisedBy { get; set; }
}

public enum PlanItemStatus
{
    Planned,
    InProgress,
    Awarded,
    Deferred,
    Cancelled
}

/// <summary>Annual procurement plan / demand plan line generated from approved projects (FR-BUD-003/004/009).</summary>
public class ProcurementPlanItem : AuditableEntity
{
    public string FinancialYear { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid? BudgetLineId { get; set; }
    public string Description { get; set; } = default!;
    public decimal EstimatedValue { get; set; }
    public string? PlannedMethod { get; set; }
    public DateOnly? PlannedRequisitionDate { get; set; }
    public DateOnly? PlannedAdvertDate { get; set; }
    public DateOnly? PlannedAwardDate { get; set; }
    public DateOnly? ActualRequisitionDate { get; set; }
    public DateOnly? ActualAdvertDate { get; set; }
    public DateOnly? ActualAwardDate { get; set; }
    public decimal? ActualValue { get; set; }
    public PlanItemStatus Status { get; set; } = PlanItemStatus.Planned;
    public string? VarianceReason { get; set; }
    public string? OrgUnit { get; set; }
}

/// <summary>
/// Effective-dated, versioned procurement method/threshold rule (FR-BUD-007/008, BR-012). Rules are
/// maintained as configuration; a new or changed rule is inactive until approved by a second authorised user.
/// </summary>
public class ProcurementMethodRule : AuditableEntity
{
    public string MethodCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public int RuleVersion { get; set; } = 1;
    public ConfigStatus Status { get; set; } = ConfigStatus.Draft;
    public bool RequiresPublication { get; set; }
    public int MinimumQuotations { get; set; }
    public int MinimumAdvertDays { get; set; }
    public string ApprovalAuthority { get; set; } = default!;
    public string? PolicyReference { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public bool AppliesTo(decimal value, DateOnly onDate) =>
        Status == ConfigStatus.Approved
        && EffectiveFrom <= onDate && (EffectiveTo is null || EffectiveTo >= onDate)
        && value >= MinValue && (MaxValue is null || value <= MaxValue);
}
