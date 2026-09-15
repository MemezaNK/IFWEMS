using System.Security.Claims;
using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record InvestigationDto(Guid Id, Guid CaseId, Guid InvestigatorUserId, InvestigationStatus Status, string? Findings, string? Recommendation, DateTime CreatedAtUtc);
public record CreateInvestigationRequest(string? Findings, string? Recommendation);
public record ApproveInvestigationRequest(bool Approve, string? Comments);

[ApiController]
[Route("api")]
[Authorize]
public class InvestigationsController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public InvestigationsController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("cases/{caseId:guid}/investigations")]
    public async Task<ActionResult<IEnumerable<InvestigationDto>>> GetForCase(Guid caseId, CancellationToken cancellationToken)
    {
        var results = await _dbContext.Investigations
            .Where(i => i.CaseId == caseId)
            .Select(i => new InvestigationDto(i.Id, i.CaseId, i.InvestigatorUserId, i.Status, i.Findings, i.Recommendation, i.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(results);
    }

    /// <summary>Perform (maker) an investigation against a case. FR-003, FR-023.</summary>
    [HttpPost("cases/{caseId:guid}/investigations")]
    [Authorize(Roles = "Investigator,SystemAdministrator")]
    public async Task<ActionResult<InvestigationDto>> Create(Guid caseId, [FromBody] CreateInvestigationRequest request, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Cases.AnyAsync(c => c.Id == caseId, cancellationToken))
        {
            return NotFound();
        }

        var entity = new Investigation
        {
            CaseId = caseId,
            InvestigatorUserId = GetCurrentUserId(),
            Findings = request.Findings,
            Recommendation = request.Recommendation,
            Status = InvestigationStatus.PendingApproval
        };

        _dbContext.Investigations.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new InvestigationDto(entity.Id, entity.CaseId, entity.InvestigatorUserId, entity.Status, entity.Findings, entity.Recommendation, entity.CreatedAtUtc);
        return CreatedAtAction(nameof(GetForCase), new { caseId }, dto);
    }

    /// <summary>Approve (checker) an investigation outcome, enforcing maker/checker segregation. FR-003, FR-023.</summary>
    [HttpPost("investigations/{id:guid}/approve")]
    [Authorize(Roles = "ApprovingOfficial,SystemAdministrator")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveInvestigationRequest request, CancellationToken cancellationToken)
    {
        var investigation = await _dbContext.Investigations.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (investigation is null) return NotFound();

        var approverId = GetCurrentUserId();
        if (investigation.InvestigatorUserId == approverId)
        {
            return Conflict(new { message = "The investigator cannot approve their own investigation (segregation of duties, FR-003)." });
        }

        investigation.Status = request.Approve ? InvestigationStatus.Approved : InvestigationStatus.Rejected;
        investigation.ApprovedByUserId = approverId;
        investigation.ApprovedAtUtc = DateTime.UtcNow;

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
