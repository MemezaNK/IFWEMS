using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record ControlDto(Guid Id, string Code, string Name, string Description, bool IsActive);
public record CreateControlRequest(string Code, string Name, string Description);

[ApiController]
[Route("api/controls")]
[Authorize]
public class ControlsController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public ControlsController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ControlDto>>> GetAll(CancellationToken cancellationToken)
    {
        var controls = await _dbContext.Controls
            .Where(c => c.IsActive)
            .Select(c => new ControlDto(c.Id, c.Code, c.Name, c.Description, c.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(controls);
    }

    /// <summary>Manage the control library that confirmed incidents are linked to. FR-026.</summary>
    [HttpPost]
    [Authorize(Roles = "ComplianceOfficer,SystemAdministrator")]
    public async Task<ActionResult<ControlDto>> Create([FromBody] CreateControlRequest request, CancellationToken cancellationToken)
    {
        var entity = new Control { Code = request.Code, Name = request.Name, Description = request.Description };
        _dbContext.Controls.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ControlDto(entity.Id, entity.Code, entity.Name, entity.Description, entity.IsActive);
        return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, dto);
    }
}
