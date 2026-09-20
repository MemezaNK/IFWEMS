using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record TransactionDto(
    Guid Id,
    string TransactionReference,
    Guid OrgUnitId,
    string OrgUnitName,
    Guid? SupplierId,
    string? SupplierName,
    decimal Amount,
    int RiskScore,
    string RiskRating,
    string RecommendedAction,
    IReadOnlyList<string> FailedRuleCodes,
    bool IsEmergencyOverride,
    string? OverrideReason,
    DateTime CreatedAtUtc);

public record TransactionPageDto(IReadOnlyList<TransactionDto> Items, int TotalCount, int Page, int PageSize);

/// <summary>
/// Read-only screening register: every transaction evaluated by the compliance rule engine
/// (both routine checks and emergency overrides), with its outcome. Section 6.3
/// (financial_transaction) / M05 Transaction Screening -- the register the preventive
/// screening workflow (Section 32) is meant to leave behind, previously invisible because
/// nothing surfaced it via API/UI.
/// </summary>
[ApiController]
[Route("api/transactions")]
[Authorize(Roles = "ComplianceOfficer,ApprovingOfficial,ReadOnlyAuditor,SystemAdministrator")]
public class TransactionsController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public TransactionsController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<TransactionPageDto>> GetAll(
        [FromQuery] string? riskRating,
        [FromQuery] Guid? orgUnitId,
        [FromQuery] Guid? supplierId,
        [FromQuery] bool? emergencyOverrideOnly,
        [FromQuery] string? search,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 || pageSize > 200 ? 50 : pageSize;

        var query = _dbContext.Transactions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(riskRating))
        {
            query = query.Where(t => t.RiskRating == riskRating);
        }
        if (orgUnitId.HasValue)
        {
            query = query.Where(t => t.OrgUnitId == orgUnitId.Value);
        }
        if (supplierId.HasValue)
        {
            query = query.Where(t => t.SupplierId == supplierId.Value);
        }
        if (emergencyOverrideOnly == true)
        {
            query = query.Where(t => t.IsEmergencyOverride);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t => t.TransactionReference.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TransactionDto(
                t.Id, t.TransactionReference, t.OrgUnitId, t.OrgUnit.Name,
                t.SupplierId, t.Supplier != null ? t.Supplier.Name : null,
                t.Amount, t.RiskScore, t.RiskRating, t.RecommendedAction.ToString(),
                t.FailedRuleCodes == null || t.FailedRuleCodes == string.Empty
                    ? new List<string>()
                    : t.FailedRuleCodes.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                t.IsEmergencyOverride, t.OverrideReason, t.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new TransactionPageDto(items, totalCount, page, pageSize));
    }
}
