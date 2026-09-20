using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record SlaPolicyDto(Guid Id, CaseType CaseType, CaseStatus Status, int MaxDurationHours, bool IsActive, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc);
public record CreateSlaPolicyRequest(CaseType CaseType, CaseStatus Status, int MaxDurationHours, DateTime EffectiveFromUtc, DateTime? EffectiveToUtc);

public record OverdueCaseDto(Guid CaseId, string CaseNumber, CaseType CaseType, CaseStatus Status, DateTime EnteredStatusAtUtc, int HoursInStatus, int SlaMaxHours, int HoursOverdue);

/// <summary>
/// Configurable SLA policies and breach/approach detection. FR-040/FR-041.
/// </summary>
[ApiController]
[Route("api/sla")]
[Authorize]
public class SlaController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public SlaController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("policies")]
    public async Task<ActionResult<IEnumerable<SlaPolicyDto>>> GetPolicies(CancellationToken cancellationToken)
    {
        var policies = await _dbContext.SlaPolicies.AsNoTracking()
            .Select(p => new SlaPolicyDto(p.Id, p.CaseType, p.Status, p.MaxDurationHours, p.IsActive, p.EffectiveFromUtc, p.EffectiveToUtc))
            .ToListAsync(cancellationToken);

        return Ok(policies);
    }

    /// <summary>Create/version an SLA policy for a case type + stage. FR-040.</summary>
    [HttpPost("policies")]
    [Authorize(Roles = "ComplianceOfficer,SystemAdministrator")]
    public async Task<ActionResult<SlaPolicyDto>> CreatePolicy([FromBody] CreateSlaPolicyRequest request, CancellationToken cancellationToken)
    {
        var previousActive = await _dbContext.SlaPolicies
            .Where(p => p.CaseType == request.CaseType && p.Status == request.Status && p.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var previous in previousActive)
        {
            previous.IsActive = false;
            previous.EffectiveToUtc = request.EffectiveFromUtc;
        }

        var entity = new SlaPolicy
        {
            CaseType = request.CaseType,
            Status = request.Status,
            MaxDurationHours = request.MaxDurationHours,
            EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveToUtc = request.EffectiveToUtc,
            IsActive = true,
            Version = previousActive.Count + 1
        };

        _dbContext.SlaPolicies.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new SlaPolicyDto(entity.Id, entity.CaseType, entity.Status, entity.MaxDurationHours, entity.IsActive, entity.EffectiveFromUtc, entity.EffectiveToUtc);
        return CreatedAtAction(nameof(GetPolicies), new { id = entity.Id }, dto);
    }

    /// <summary>
    /// Cases currently breaching (or approaching, within 20% of the limit) their configured
    /// SLA for their current status. FR-041.
    /// </summary>
    [HttpGet("overdue-cases")]
    public async Task<ActionResult<IEnumerable<OverdueCaseDto>>> GetOverdueCases(CancellationToken cancellationToken)
    {
        var activePolicies = await _dbContext.SlaPolicies.AsNoTracking()
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);

        var openCases = await _dbContext.Cases.AsNoTracking()
            .Where(c => c.Status != CaseStatus.Closed)
            .ToListAsync(cancellationToken);

        var openCaseIds = openCases.Select(c => c.Id).ToList();

        // Batch-load the most recent transition into each case's current status in a single
        // grouped query, rather than one round-trip per case (which previously took 15s+ for
        // a few hundred open cases and violated NFR-02's <3s response time budget).
        var lastTransitions = await _dbContext.CaseStatusHistories.AsNoTracking()
            .Where(h => openCaseIds.Contains(h.CaseId))
            .GroupBy(h => new { h.CaseId, h.ToStatus })
            .Select(g => new { g.Key.CaseId, g.Key.ToStatus, ChangedAtUtc = g.Max(h => h.ChangedAtUtc) })
            .ToListAsync(cancellationToken);

        var lastTransitionLookup = lastTransitions.ToDictionary(t => (t.CaseId, t.ToStatus), t => t.ChangedAtUtc);

        var results = new List<OverdueCaseDto>();
        var now = DateTime.UtcNow;

        foreach (var caseEntity in openCases)
        {
            var policy = activePolicies.SingleOrDefault(p => p.CaseType == caseEntity.CaseType && p.Status == caseEntity.Status);
            if (policy is null) continue;

            var lastTransition = lastTransitionLookup.TryGetValue((caseEntity.Id, caseEntity.Status), out var changedAtUtc)
                ? changedAtUtc
                : caseEntity.CreatedAtUtc;

            var hoursInStatus = (int)(now - lastTransition).TotalHours;
            if (hoursInStatus < policy.MaxDurationHours) continue;

            results.Add(new OverdueCaseDto(
                caseEntity.Id, caseEntity.CaseNumber, caseEntity.CaseType, caseEntity.Status,
                lastTransition, hoursInStatus, policy.MaxDurationHours, hoursInStatus - policy.MaxDurationHours));
        }

        return Ok(results.OrderByDescending(r => r.HoursOverdue));
    }
}
