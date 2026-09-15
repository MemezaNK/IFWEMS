using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers.Admin;

public record UserDto(Guid Id, string Username, string Email, string DisplayName, bool IsActive, IReadOnlyList<string> Roles);
public record CreateUserRequest(string Username, string Email, string DisplayName, string Password, Guid? OrgUnitId, List<string> RoleNames);

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "SystemAdministrator")]
public class UsersController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public UsersController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Select(u => new UserDto(
                u.Id, u.Username, u.Email, u.DisplayName, u.IsActive,
                u.UserRoles.Select(ur => ur.Role.Name).ToList()))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .Where(r => request.RoleNames.Contains(r.Name))
            .ToListAsync(cancellationToken);

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            DisplayName = request.DisplayName,
            OrgUnitId = request.OrgUnitId
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = new UserDto(user.Id, user.Username, user.Email, user.DisplayName, user.IsActive, roles.Select(r => r.Name).ToList());
        return CreatedAtAction(nameof(GetAll), new { id = user.Id }, result);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FindAsync(new object?[] { id }, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
