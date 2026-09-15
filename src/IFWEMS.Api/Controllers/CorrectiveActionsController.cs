using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record CorrectiveActionDto(Guid Id, Guid CaseId, Guid ControlId, string Description, CorrectiveActionStatus Status, Guid? EvidenceDocumentId, string? VerifiedBy);
public record CreateCorrectiveActionRequest(Guid ControlId, string Description);
public record VerifyCorrectiveActionRequest(Guid EvidenceDocumentId);

[ApiController]
[Route("api")]
[Authorize]
public class CorrectiveActionsController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public CorrectiveActionsController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("cases/{caseId:guid}/corrective-actions")]
    public async Task<ActionResult<IEnumerable<CorrectiveActionDto>>> GetForCase(Guid caseId, CancellationToken cancellationToken)
    {
        var results = await _dbContext.CorrectiveActions
            .Where(a => a.CaseId == caseId)
            .Select(a => new CorrectiveActionDto(a.Id, a.CaseId, a.ControlId, a.Description, a.Status, a.EvidenceDocumentId, a.VerifiedBy))
            .ToListAsync(cancellationToken);

        return Ok(results);
    }

    /// <summary>Link a confirmed incident's corrective action to the control library. FR-026.</summary>
    [HttpPost("cases/{caseId:guid}/corrective-actions")]
    [Authorize(Roles = "CaseReviewer,ApprovingOfficial,SystemAdministrator")]
    public async Task<ActionResult<CorrectiveActionDto>> Create(Guid caseId, [FromBody] CreateCorrectiveActionRequest request, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Cases.AnyAsync(c => c.Id == caseId, cancellationToken)) return NotFound();
        if (!await _dbContext.Controls.AnyAsync(c => c.Id == request.ControlId, cancellationToken))
        {
            return BadRequest(new { message = "The referenced control does not exist." });
        }

        var entity = new CorrectiveAction
        {
            CaseId = caseId,
            ControlId = request.ControlId,
            Description = request.Description
        };

        _dbContext.CorrectiveActions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new CorrectiveActionDto(entity.Id, entity.CaseId, entity.ControlId, entity.Description, entity.Status, entity.EvidenceDocumentId, entity.VerifiedBy);
        return CreatedAtAction(nameof(GetForCase), new { caseId }, dto);
    }

    /// <summary>
    /// Verify implementation with evidence before the case can be closed. FR-026 requires
    /// verified evidence of corrective-action implementation before case closure.
    /// </summary>
    [HttpPost("corrective-actions/{id:guid}/verify")]
    [Authorize(Roles = "ApprovingOfficial,SystemAdministrator")]
    public async Task<IActionResult> Verify(Guid id, [FromBody] VerifyCorrectiveActionRequest request, CancellationToken cancellationToken)
    {
        var action = await _dbContext.CorrectiveActions.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (action is null) return NotFound();

        if (!await _dbContext.Documents.AnyAsync(d => d.Id == request.EvidenceDocumentId, cancellationToken))
        {
            return BadRequest(new { message = "The referenced evidence document does not exist." });
        }

        action.EvidenceDocumentId = request.EvidenceDocumentId;
        action.Status = CorrectiveActionStatus.Verified;
        action.VerifiedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        action.VerifiedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
