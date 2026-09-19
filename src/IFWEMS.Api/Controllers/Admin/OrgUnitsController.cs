using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers.Admin;

public record OrgUnitDto(Guid Id, string Code, string Name, string Level, Guid? ParentOrgUnitId, bool IsActive);

[ApiController]
[Route("api/admin/org-units")]
[Authorize]
public class OrgUnitsController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public OrgUnitsController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrgUnitDto>>> GetAll(CancellationToken cancellationToken)
    {
        var orgUnits = await _dbContext.OrgUnits
            .Where(o => o.IsActive)
            .Select(o => new OrgUnitDto(o.Id, o.Code, o.Name, o.Level, o.ParentOrgUnitId, o.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(orgUnits);
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<ActionResult<OrgUnitDto>> Create([FromBody] OrgUnitDto dto, CancellationToken cancellationToken)
    {
        var entity = new OrgUnit
        {
            Code = dto.Code,
            Name = dto.Name,
            Level = dto.Level,
            ParentOrgUnitId = dto.ParentOrgUnitId
        };

        _dbContext.OrgUnits.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = new OrgUnitDto(entity.Id, entity.Code, entity.Name, entity.Level, entity.ParentOrgUnitId, entity.IsActive);
        return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, result);
    }
}
