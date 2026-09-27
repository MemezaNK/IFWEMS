using Platform.Core;
using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Strategy;

public enum PlanStatus
{
    Draft,
    Submitted,
    Approved,
    Superseded
}

/// <summary>Multi-year strategic plan / planning period (FR-STR-001). Versioned: approved baselines are snapshotted immutably (FR-STR-007).</summary>
public class StrategicPlan : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int VersionNumber { get; set; } = 1;
    public PlanStatus Status { get; set; } = PlanStatus.Draft;
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }

    public ICollection<StrategicOutcome> Outcomes { get; set; } = new List<StrategicOutcome>();
    public ICollection<StrategicObjective> Objectives { get; set; } = new List<StrategicObjective>();
    public ICollection<StrategicPlanVersion> Versions { get; set; } = new List<StrategicPlanVersion>();

    /// <summary>Only draft plans can be edited; approved content changes go through a new revision.</summary>
    public bool IsEditable => Status == PlanStatus.Draft;

    public void EnsureEditable()
    {
        if (!IsEditable)
            throw new DomainException($"Strategic plan {Code} v{VersionNumber} is {Status} and locked. Start a revision to change it.", "FR-STR-007");
    }
}

/// <summary>Immutable snapshot of an approved plan version (FR-STR-007: historic version remains immutable).</summary>
public class StrategicPlanVersion : Entity
{
    public Guid PlanId { get; set; }
    public int VersionNumber { get; set; }
    public string SnapshotJson { get; set; } = default!;
    public DateTime ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }
    public string? ChangeSummary { get; set; }
}

public class StrategicOutcome : AuditableEntity
{
    public Guid PlanId { get; set; }
    public string Code { get; set; } = default!;
    public string Description { get; set; } = default!;
}

/// <summary>Strategic objective under a plan (and optionally an outcome) — FR-STR-002.</summary>
public class StrategicObjective : AuditableEntity
{
    public Guid PlanId { get; set; }
    public Guid? OutcomeId { get; set; }
    public string Code { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? OwnerName { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? ProgrammeName { get; set; }

    public ICollection<AppIndicator> Indicators { get; set; } = new List<AppIndicator>();
}

/// <summary>APP performance indicator with its mandatory evidence rule (FR-STR-002, FR-STR-006).</summary>
public class AppIndicator : AuditableEntity
{
    public Guid ObjectiveId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string UnitOfMeasure { get; set; } = "Number";
    public string Measure { get; set; } = "Sum of verified project results";
    public string EvidenceRule { get; set; } = default!;

    /// <summary>Comma-separated evidence types that must be verified before a result is final (BR-009).</summary>
    public string RequiredEvidenceTypes { get; set; } = string.Empty;
    public string? ResponsibleExecutive { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public bool IsCumulative { get; set; } = true;

    public ICollection<AppTarget> Targets { get; set; } = new List<AppTarget>();

    public IReadOnlyList<string> RequiredEvidenceTypeList() =>
        RequiredEvidenceTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public enum TargetStatus
{
    Draft,
    Approved,
    Locked
}

/// <summary>Annual (Quarter = 0) or quarterly APP target (FR-STR-003) with forecast and commentary (FR-STR-008).</summary>
public class AppTarget : AuditableEntity
{
    public Guid IndicatorId { get; set; }
    public string FinancialYear { get; set; } = default!;
    public int Quarter { get; set; }
    public decimal TargetValue { get; set; }
    public decimal? ForecastValue { get; set; }
    public string? ForecastCommentary { get; set; }
    public TargetStatus Status { get; set; } = TargetStatus.Draft;
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }

    public void EnsureEditable()
    {
        if (Status != TargetStatus.Draft)
            throw new DomainException("Approved/locked APP targets cannot be edited.", "FR-STR-003");
    }
}

public enum ContributionMethod
{
    /// <summary>Project result counts in full towards the indicator.</summary>
    Direct,

    /// <summary>Project result is multiplied by a weight/proportion.</summary>
    Weighted,

    /// <summary>Each verified result contributes one unit (e.g. a completed intervention).</summary>
    Count
}

/// <summary>Project ↔ APP indicator alignment and contribution rule (FR-STR-004, FR-STR-005).</summary>
public class ProjectIndicatorLink : Entity
{
    public Guid ProjectId { get; set; }
    public Guid IndicatorId { get; set; }
    public ContributionMethod Method { get; set; } = ContributionMethod.Direct;
    public decimal Weight { get; set; } = 1m;
    public decimal? PlannedContribution { get; set; }

    public decimal ContributionOf(decimal resultValue) => Method switch
    {
        ContributionMethod.Direct => resultValue,
        ContributionMethod.Weighted => Math.Round(resultValue * Weight, 4),
        ContributionMethod.Count => 1m,
        _ => resultValue
    };
}

public enum ResultStatus
{
    Captured,
    Submitted,
    Verified,
    Rejected
}

/// <summary>A project's reported output against an APP indicator for a period. Final only once verified with evidence (BR-009).</summary>
public class PerformanceResult : AuditableEntity
{
    public Guid IndicatorId { get; set; }
    public Guid ProjectId { get; set; }
    public string FinancialYear { get; set; } = default!;
    public int Quarter { get; set; }
    public decimal Value { get; set; }
    public string? Narrative { get; set; }
    public ResultStatus Status { get; set; } = ResultStatus.Captured;
    public DateTime? VerifiedAtUtc { get; set; }
    public string? VerifiedBy { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public string? VerificationComment { get; set; }

    public bool IsFinal => Status == ResultStatus.Verified;
}
