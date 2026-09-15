using IFWEMS.Domain.Entities;

namespace IFWEMS.Application.Common.Interfaces;

/// <summary>
/// Generates unique, centrally issued, non-reusable case numbers: [DEPT]-[TYPE]-[FY]-[SEQUENCE]. FR-020.
/// </summary>
public interface ICaseNumberGenerator
{
    Task<string> GenerateAsync(CaseType caseType, CancellationToken cancellationToken = default);
}
