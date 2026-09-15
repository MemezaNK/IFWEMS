using System.Security.Claims;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record NotificationDto(Guid Id, string Subject, string Body, bool IsRead, DateTime? SentAtUtc);

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public NotificationsController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetMine(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var notifications = await _dbContext.Notifications
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new NotificationDto(n.Id, n.Subject, n.Body, n.IsRead, n.SentAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(notifications);
    }

    [HttpPost("{id:guid}/mark-read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var notification = await _dbContext.Notifications
            .SingleOrDefaultAsync(n => n.Id == id && n.RecipientUserId == userId, cancellationToken);

        if (notification is null)
        {
            return NotFound();
        }

        notification.IsRead = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated request is missing a user identifier claim.");
        return Guid.Parse(idClaim);
    }
}
