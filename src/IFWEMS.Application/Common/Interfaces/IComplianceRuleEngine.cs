using IFWEMS.Application.Compliance;

namespace IFWEMS.Application.Common.Interfaces;

/// <summary>
/// Real-time compliance screening engine. FR-010.
/// </summary>
public interface IComplianceRuleEngine
{
    Task<TransactionCheckResult> EvaluateAsync(TransactionCheckRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Controlled emergency-override workflow. FR-013/FR-014.
/// </summary>
public interface IEmergencyOverrideService
{
    Task<Guid> CaptureOverrideAsync(EmergencyOverrideRequest request, string? approvedBy, CancellationToken cancellationToken = default);
}
