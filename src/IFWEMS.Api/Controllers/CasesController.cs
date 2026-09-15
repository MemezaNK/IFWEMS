using System.Security.Claims;
using IFWEMS.Application.Cases;
using IFWEMS.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IFWEMS.Api.Controllers;

[ApiController]
[Route("api/cases")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly ICaseService _caseService;

    public CasesController(ICaseService caseService)
    {
        _caseService = caseService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CaseDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _caseService.GetCasesAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CaseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _caseService.GetCaseAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Capture a new case. Requires the cases.create permission (FR-003 maker role).</summary>
    [HttpPost]
    [Authorize(Roles = "CaseOfficer,SystemAdministrator")]
    public async Task<ActionResult<CaseDto>> Create([FromBody] CreateCaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _caseService.CreateCaseAsync(request, CurrentUsername, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Transition a case's lifecycle status, recording an immutable status-history entry (FR-024).</summary>
    [HttpPost("{id:guid}/status")]
    [Authorize(Roles = "CaseReviewer,ApprovingOfficial,SystemAdministrator")]
    public async Task<ActionResult<CaseDto>> ChangeStatus(Guid id, [FromBody] ChangeCaseStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _caseService.ChangeStatusAsync(id, request.NewStatus, request.Reason, CurrentUsername, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private string? CurrentUsername => User.FindFirstValue(ClaimTypes.Name);
}
