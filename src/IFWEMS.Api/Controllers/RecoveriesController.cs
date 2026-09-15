using System.Security.Claims;
using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record RecoveryDto(Guid Id, Guid CaseId, decimal Amount, RecoveryStatus Status, string? Reference, DateTime CreatedAtUtc);
public record CaptureRecoveryRequest(decimal Amount, string? Reference);
public record ApproveWriteOffRequest(string Reason);

[ApiController]
[Route("api")]
[Authorize]
public class RecoveriesController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public RecoveriesController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("cases/{caseId:guid}/recoveries")]
    public async Task<ActionResult<IEnumerable<RecoveryDto>>> GetForCase(Guid caseId, CancellationToken cancellationToken)
    {
        var results = await _dbContext.Recoveries
            .Where(r => r.CaseId == caseId)
            .Select(r => new RecoveryDto(r.Id, r.CaseId, r.Amount, r.Status, r.Reference, r.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(results);
    }

    /// <summary>Capture an individual recovery transaction, reconciled against the case's recoverable balance. FR-025.</summary>
    [HttpPost("cases/{caseId:guid}/recoveries")]
    [Authorize(Roles = "FinanceOfficer,SystemAdministrator")]
    public async Task<ActionResult<RecoveryDto>> Capture(Guid caseId, [FromBody] CaptureRecoveryRequest request, CancellationToken cancellationToken)
    {
        var caseEntity = await _dbContext.Cases.SingleOrDefaultAsync(c => c.Id == caseId, cancellationToken);
        if (caseEntity is null) return NotFound();

        var outstanding = caseEntity.RecoverableAmount - caseEntity.RecoveredAmount;
        if (request.Amount > outstanding)
        {
            return Conflict(new { message = $"Recovery amount {request.Amount:C} exceeds the outstanding recoverable balance of {outstanding:C}." });
        }

        var entity = new Recovery
        {
            CaseId = caseId,
            Amount = request.Amount,
            Reference = request.Reference,
            CapturedByUserId = GetCurrentUserId(),
            Status = RecoveryStatus.Recovered
        };

        _dbContext.Recoveries.Add(entity);
        caseEntity.RecoveredAmount += request.Amount;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new RecoveryDto(entity.Id, entity.CaseId, entity.Amount, entity.Status, entity.Reference, entity.CreatedAtUtc);
        return CreatedAtAction(nameof(GetForCase), new { caseId }, dto);
    }

    /// <summary>Approve write-off of an outstanding recoverable balance (checker role). FR-003, FR-025.</summary>
    [HttpPost("recoveries/{id:guid}/write-off")]
    [Authorize(Roles = "ApprovingOfficial,SystemAdministrator")]
    public async Task<IActionResult> ApproveWriteOff(Guid id, [FromBody] ApproveWriteOffRequest request, CancellationToken cancellationToken)
    {
        var recovery = await _dbContext.Recoveries.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (recovery is null) return NotFound();

        recovery.Status = RecoveryStatus.WrittenOff;
        recovery.WriteOffReason = request.Reason;
        recovery.ApprovedByUserId = GetCurrentUserId();
        recovery.ApprovedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated request is missing a user identifier claim.");
        return Guid.Parse(idClaim);
    }
}
