using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers.Admin;

public record RoleDto(Guid Id, string Name, string? Description, IReadOnlyList<string> Permissions);
public record CreateRoleRequest(string Name, string? Description, List<string> PermissionCodes);

[ApiController]
[Route("api/admin/roles")]
[Authorize(Roles = "SystemAdministrator")]
public class RolesController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public RolesController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetAll(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.RolePermissions.Select(rp => rp.Permission.Code).ToList()))
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }

    [HttpPost]
    public async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var permissions = await _dbContext.Permissions
            .Where(p => request.PermissionCodes.Contains(p.Code))
            .ToListAsync(cancellationToken);

        var role = new Role { Name = request.Name, Description = request.Description };
        foreach (var permission in permissions)
        {
            role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = new RoleDto(role.Id, role.Name, role.Description, permissions.Select(p => p.Code).ToList());
        return CreatedAtAction(nameof(GetAll), new { id = role.Id }, result);
    }
}
