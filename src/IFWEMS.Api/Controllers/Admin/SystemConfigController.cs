using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers.Admin;

public record SystemSettingDto(Guid Id, string Key, string Value, string Category, string? Description, DateTime? ModifiedAtUtc);
public record CreateSystemSettingRequest(string Key, string Value, string Category, string? Description);
public record UpdateSystemSettingRequest(string Value, string? Description);

/// <summary>
/// Self-contained system configuration console (PLAT-04). Lets a SystemAdministrator manage
/// key/value system-wide parameters without requiring a redeploy or external config service.
/// </summary>
[ApiController]
[Route("api/admin/system-config")]
[Authorize(Roles = "SystemAdministrator")]
public class SystemConfigController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public SystemConfigController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SystemSettingDto>>> GetAll(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SystemSettings
            .OrderBy(s => s.Category).ThenBy(s => s.Key)
            .Select(s => new SystemSettingDto(s.Id, s.Key, s.Value, s.Category, s.Description, s.ModifiedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(settings);
    }

    [HttpPost]
    public async Task<ActionResult<SystemSettingDto>> Create([FromBody] CreateSystemSettingRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new { message = "Key and category are required." });
        }

        var exists = await _dbContext.SystemSettings.AnyAsync(s => s.Key == request.Key, cancellationToken);
        if (exists)
        {
            return Conflict(new { message = $"A setting with key '{request.Key}' already exists." });
        }

        var setting = new SystemSetting
        {
            Key = request.Key,
            Value = request.Value,
            Category = request.Category,
            Description = request.Description
        };

        _dbContext.SystemSettings.Add(setting);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = new SystemSettingDto(setting.Id, setting.Key, setting.Value, setting.Category, setting.Description, setting.ModifiedAtUtc);
        return CreatedAtAction(nameof(GetAll), new { id = setting.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SystemSettingDto>> Update(Guid id, [FromBody] UpdateSystemSettingRequest request, CancellationToken cancellationToken)
    {
        var setting = await _dbContext.SystemSettings.FindAsync(new object?[] { id }, cancellationToken);
        if (setting is null)
        {
            return NotFound();
        }

        setting.Value = request.Value;
        setting.Description = request.Description;
        setting.ModifiedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new SystemSettingDto(setting.Id, setting.Key, setting.Value, setting.Category, setting.Description, setting.ModifiedAtUtc));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var setting = await _dbContext.SystemSettings.FindAsync(new object?[] { id }, cancellationToken);
        if (setting is null)
        {
            return NotFound();
        }

        _dbContext.SystemSettings.Remove(setting);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
