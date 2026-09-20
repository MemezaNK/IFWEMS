using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers.Admin;

public record NotificationTemplateDto(Guid Id, string Code, string Subject, string BodyHtml, bool IsActive, DateTime? ModifiedAtUtc);
public record CreateNotificationTemplateRequest(string Code, string Subject, string BodyHtml, bool IsActive);
public record UpdateNotificationTemplateRequest(string Subject, string BodyHtml, bool IsActive);

/// <summary>
/// In-app-editable email/notification template management console. FR-005.
/// </summary>
[ApiController]
[Route("api/admin/notification-templates")]
[Authorize(Roles = "SystemAdministrator")]
public class NotificationTemplatesController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public NotificationTemplatesController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationTemplateDto>>> GetAll(CancellationToken cancellationToken)
    {
        var templates = await _dbContext.NotificationTemplates
            .OrderBy(t => t.Code)
            .Select(t => new NotificationTemplateDto(t.Id, t.Code, t.Subject, t.BodyHtml, t.IsActive, t.ModifiedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(templates);
    }

    [HttpPost]
    public async Task<ActionResult<NotificationTemplateDto>> Create([FromBody] CreateNotificationTemplateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.BodyHtml))
        {
            return BadRequest(new { message = "Code, subject and body are required." });
        }

        var exists = await _dbContext.NotificationTemplates.AnyAsync(t => t.Code == request.Code, cancellationToken);
        if (exists)
        {
            return Conflict(new { message = $"A template with code '{request.Code}' already exists." });
        }

        var template = new NotificationTemplate
        {
            Code = request.Code,
            Subject = request.Subject,
            BodyHtml = request.BodyHtml,
            IsActive = request.IsActive
        };

        _dbContext.NotificationTemplates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = new NotificationTemplateDto(template.Id, template.Code, template.Subject, template.BodyHtml, template.IsActive, template.ModifiedAtUtc);
        return CreatedAtAction(nameof(GetAll), new { id = template.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NotificationTemplateDto>> Update(Guid id, [FromBody] UpdateNotificationTemplateRequest request, CancellationToken cancellationToken)
    {
        var template = await _dbContext.NotificationTemplates.FindAsync(new object?[] { id }, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        template.Subject = request.Subject;
        template.BodyHtml = request.BodyHtml;
        template.IsActive = request.IsActive;
        template.ModifiedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new NotificationTemplateDto(template.Id, template.Code, template.Subject, template.BodyHtml, template.IsActive, template.ModifiedAtUtc));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var template = await _dbContext.NotificationTemplates.FindAsync(new object?[] { id }, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        _dbContext.NotificationTemplates.Remove(template);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
