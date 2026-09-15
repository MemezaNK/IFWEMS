using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record AssessmentDto(Guid Id, Guid CaseId, Guid AssessorUserId, string AnswersJson, string Conclusion, string? Reasons, DateTime CreatedAtUtc);
public record CreateAssessmentRequest(string AnswersJson, string Conclusion, string? Reasons);

[ApiController]
[Route("api/cases/{caseId:guid}/assessments")]
[Authorize]
public class CaseAssessmentsController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public CaseAssessmentsController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AssessmentDto>>> GetAll(Guid caseId, CancellationToken cancellationToken)
    {
        var assessments = await _dbContext.CaseAssessments
            .Where(a => a.CaseId == caseId)
            .Select(a => new AssessmentDto(a.Id, a.CaseId, a.AssessorUserId, a.AnswersJson, a.Conclusion, a.Reasons, a.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(assessments);
    }

    /// <summary>Capture a structured assessment questionnaire response against a case. FR-022.</summary>
    [HttpPost]
    [Authorize(Roles = "CaseOfficer,SystemAdministrator")]
    public async Task<ActionResult<AssessmentDto>> Create(Guid caseId, [FromBody] CreateAssessmentRequest request, CancellationToken cancellationToken)
    {
        var caseExists = await _dbContext.Cases.AnyAsync(c => c.Id == caseId, cancellationToken);
        if (!caseExists) return NotFound();

        var assessorId = GetCurrentUserId();
        var entity = new CaseAssessment
        {
            CaseId = caseId,
            AssessorUserId = assessorId,
            AnswersJson = request.AnswersJson,
            Conclusion = request.Conclusion,
            Reasons = request.Reasons
        };

        _dbContext.CaseAssessments.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new AssessmentDto(entity.Id, entity.CaseId, entity.AssessorUserId, entity.AnswersJson, entity.Conclusion, entity.Reasons, entity.CreatedAtUtc);
        return CreatedAtAction(nameof(GetAll), new { caseId }, dto);
    }

    private Guid GetCurrentUserId()
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("Authenticated request is missing a user identifier claim.");
        return Guid.Parse(idClaim);
    }
}
