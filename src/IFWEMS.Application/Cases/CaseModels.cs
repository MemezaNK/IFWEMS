using IFWEMS.Domain.Entities;

namespace IFWEMS.Application.Cases;

public record CreateCaseRequest(CaseType CaseType, Guid OrgUnitId, string Title, string? Description, decimal? AmountInvolved, Guid? SupplierId, Guid? ContractId);

public record CaseDto(
    Guid Id, string CaseNumber, CaseType CaseType, CaseStatus Status, Guid OrgUnitId,
    string Title, string? Description, decimal? AmountInvolved, decimal RecoverableAmount,
    decimal RecoveredAmount, DateTime CreatedAtUtc);

public record ChangeCaseStatusRequest(CaseStatus NewStatus, string? Reason);
