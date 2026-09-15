using IFWEMS.Application.Cases;
using IFWEMS.Application.Common.Interfaces;
using IFWEMS.Application.Compliance;
using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;

namespace IFWEMS.Infrastructure.Compliance;

/// <summary>
/// Captures a controlled emergency-override transaction and automatically raises a
/// post-transaction compliance review case. FR-013/FR-014.
/// </summary>
public class EmergencyOverrideService : IEmergencyOverrideService
{
    private readonly IfwemsDbContext _dbContext;
    private readonly ICaseService _caseService;

    public EmergencyOverrideService(IfwemsDbContext dbContext, ICaseService caseService)
    {
        _dbContext = dbContext;
        _caseService = caseService;
    }

    public async Task<Guid> CaptureOverrideAsync(EmergencyOverrideRequest request, string? approvedBy, CancellationToken cancellationToken = default)
    {
        var transaction = new Transaction
        {
            TransactionReference = request.TransactionReference,
            OrgUnitId = request.OrgUnitId,
            SupplierId = request.SupplierId,
            Amount = request.Amount,
            RiskRating = "AMBER",
            RecommendedAction = ComplianceAction.Review,
            IsEmergencyOverride = true,
            OverrideReason = $"[{request.EmergencyCategory}] {request.Reason}",
            OverrideApprovedBy = approvedBy
        };

        _dbContext.Transactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Automatically create a post-transaction compliance review case (FR-013).
        await _caseService.CreateCaseAsync(
            new CreateCaseRequest(
                CaseType.PotentialNonCompliance,
                request.OrgUnitId,
                $"Post-transaction review: emergency override {request.TransactionReference}",
                $"Automatically raised for review of emergency procurement override. Category: {request.EmergencyCategory}. Reason: {request.Reason}. Evidence document: {request.EvidenceDocumentId}.",
                request.Amount,
                request.SupplierId,
                null),
            approvedBy,
            cancellationToken);

        return transaction.Id;
    }
}
