using IFWEMS.Application.Cases;
using IFWEMS.Application.Common.Interfaces;
using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Infrastructure.Cases;

/// <summary>
/// Orchestrates case creation and lifecycle status transitions (FR-021/023), always
/// recording an immutable status-history entry (FR-024) and never allowing case records
/// to be deleted.
/// </summary>
public class CaseService : ICaseService
{
    private static readonly Dictionary<CaseStatus, CaseStatus[]> AllowedTransitions = new()
    {
        [CaseStatus.Draft] = new[] { CaseStatus.UnderAssessment },
        [CaseStatus.UnderAssessment] = new[] { CaseStatus.UnderInvestigation, CaseStatus.Determined },
        [CaseStatus.UnderInvestigation] = new[] { CaseStatus.Determined },
        [CaseStatus.Determined] = new[] { CaseStatus.RecoveryInProgress, CaseStatus.ConsequenceManagement, CaseStatus.CorrectiveActionPending },
        [CaseStatus.RecoveryInProgress] = new[] { CaseStatus.ConsequenceManagement, CaseStatus.CorrectiveActionPending, CaseStatus.Closed },
        [CaseStatus.ConsequenceManagement] = new[] { CaseStatus.CorrectiveActionPending, CaseStatus.Closed },
        [CaseStatus.CorrectiveActionPending] = new[] { CaseStatus.Closed },
        [CaseStatus.Closed] = Array.Empty<CaseStatus>()
    };

    private readonly IfwemsDbContext _dbContext;
    private readonly ICaseNumberGenerator _caseNumberGenerator;

    public CaseService(IfwemsDbContext dbContext, ICaseNumberGenerator caseNumberGenerator)
    {
        _dbContext = dbContext;
        _caseNumberGenerator = caseNumberGenerator;
    }

    public async Task<CaseDto> CreateCaseAsync(CreateCaseRequest request, string? createdBy, CancellationToken cancellationToken = default)
    {
        var caseNumber = await _caseNumberGenerator.GenerateAsync(request.CaseType, cancellationToken);

        var entity = new Case
        {
            CaseNumber = caseNumber,
            CaseType = request.CaseType,
            Status = CaseStatus.Draft,
            OrgUnitId = request.OrgUnitId,
            Title = request.Title,
            Description = request.Description,
            AmountInvolved = request.AmountInvolved,
            RecoverableAmount = request.AmountInvolved ?? 0m,
            SupplierId = request.SupplierId,
            ContractId = request.ContractId,
            CreatedBy = createdBy
        };

        _dbContext.Cases.Add(entity);

        _dbContext.CaseStatusHistories.Add(new CaseStatusHistory
        {
            CaseId = entity.Id,
            FromStatus = CaseStatus.Draft,
            ToStatus = CaseStatus.Draft,
            ChangedBy = createdBy,
            Reason = "Case captured."
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<CaseDto?> GetCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Cases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == caseId, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<CaseDto>> GetCasesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Cases.AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDto).ToList();
    }

    public async Task<CaseDto> ChangeStatusAsync(Guid caseId, CaseStatus newStatus, string? reason, string? changedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Cases.SingleOrDefaultAsync(c => c.Id == caseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case {caseId} was not found.");

        if (!AllowedTransitions.TryGetValue(entity.Status, out var allowed) || !allowed.Contains(newStatus))
        {
            throw new InvalidOperationException($"Cannot transition case from {entity.Status} to {newStatus}.");
        }

        var previousStatus = entity.Status;
        entity.Status = newStatus;
        entity.ModifiedBy = changedBy;
        entity.ModifiedAtUtc = DateTime.UtcNow;

        _dbContext.CaseStatusHistories.Add(new CaseStatusHistory
        {
            CaseId = entity.Id,
            FromStatus = previousStatus,
            ToStatus = newStatus,
            ChangedBy = changedBy,
            Reason = reason
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    private static CaseDto ToDto(Case entity) => new(
        entity.Id, entity.CaseNumber, entity.CaseType, entity.Status, entity.OrgUnitId,
        entity.Title, entity.Description, entity.AmountInvolved, entity.RecoverableAmount,
        entity.RecoveredAmount, entity.CreatedAtUtc);
}
