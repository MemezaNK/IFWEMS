using IFWEMS.Application.Cases;
using IFWEMS.Domain.Entities;

namespace IFWEMS.Application.Common.Interfaces;

public interface ICaseService
{
    Task<CaseDto> CreateCaseAsync(CreateCaseRequest request, string? createdBy, CancellationToken cancellationToken = default);
    Task<CaseDto?> GetCaseAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CaseDto>> GetCasesAsync(CancellationToken cancellationToken = default);
    Task<CaseDto> ChangeStatusAsync(Guid caseId, CaseStatus newStatus, string? reason, string? changedBy, CancellationToken cancellationToken = default);
}
