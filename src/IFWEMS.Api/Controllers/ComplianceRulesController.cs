using System.Security.Claims;
using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record ComplianceRuleDto(Guid Id, string Code, string Name, string Description, int Version, bool IsActive, bool IsApproved, string ParametersJson, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc);
public record CreateComplianceRuleRequest(string Code, string Name, string Description, string ParametersJson, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc);

[ApiController]
[Route("api/compliance/rules")]
[Authorize]
public class ComplianceRulesController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public ComplianceRulesController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ComplianceRuleDto>>> GetAll(CancellationToken cancellationToken)
    {
        var rules = await _dbContext.ComplianceRules
            .Select(r => new ComplianceRuleDto(r.Id, r.Code, r.Name, r.Description, r.Version, r.IsActive, r.IsApproved, r.ParametersJson, r.EffectiveFromUtc, r.EffectiveToUtc))
            .ToListAsync(cancellationToken);

        return Ok(rules);
    }

    /// <summary>Create a new (unapproved) version of a compliance rule. Requires approval before activation. FR-011.</summary>
    [HttpPost]
    [Authorize(Roles = "ComplianceOfficer,SystemAdministrator")]
    public async Task<ActionResult<ComplianceRuleDto>> Create([FromBody] CreateComplianceRuleRequest request, CancellationToken cancellationToken)
    {
        var latestVersion = await _dbContext.ComplianceRules
            .Where(r => r.Code == request.Code)
            .Select(r => (int?)r.Version)
            .MaxAsync(cancellationToken) ?? 0;

        var entity = new ComplianceRule
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            ParametersJson = request.ParametersJson,
            EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveToUtc = request.EffectiveToUtc,
            Version = latestVersion + 1,
            IsActive = false,
            IsApproved = false
        };

        _dbContext.ComplianceRules.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ComplianceRuleDto(entity.Id, entity.Code, entity.Name, entity.Description, entity.Version, entity.IsActive, entity.IsApproved, entity.ParametersJson, entity.EffectiveFromUtc, entity.EffectiveToUtc);
        return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, dto);
    }

    /// <summary>Approve (checker) and activate a compliance rule version. FR-011.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "ApprovingOfficial,SystemAdministrator")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.ComplianceRules.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule is null) return NotFound();

        // Deactivate any previously active version of the same rule code.
        var previousActive = await _dbContext.ComplianceRules
            .Where(r => r.Code == rule.Code && r.IsActive && r.Id != rule.Id)
            .ToListAsync(cancellationToken);
        foreach (var previous in previousActive)
        {
            previous.IsActive = false;
        }

        rule.IsApproved = true;
        rule.IsActive = true;
        rule.ApprovedBy = User.FindFirstValue(ClaimTypes.Name);
        rule.ApprovedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
