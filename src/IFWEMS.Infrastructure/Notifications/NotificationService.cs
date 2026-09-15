using IFWEMS.Application.Common.Interfaces;
using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IFWEMS.Infrastructure.Notifications;

/// <summary>
/// Resolves an in-app-editable NotificationTemplate, substitutes tokens, persists an in-app
/// Notification record, and dispatches email via the pluggable IEmailSender. FR-005/FR-006.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IfwemsDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IfwemsDbContext dbContext, IEmailSender emailSender, ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task SendAsync(Guid recipientUserId, string templateCode, IDictionary<string, string> tokens, CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.NotificationTemplates
            .SingleOrDefaultAsync(t => t.Code == templateCode && t.IsActive, cancellationToken);

        if (template is null)
        {
            _logger.LogWarning("Notification template {TemplateCode} not found or inactive.", templateCode);
            return;
        }

        var user = await _dbContext.Users.FindAsync(new object?[] { recipientUserId }, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("Notification recipient {UserId} not found.", recipientUserId);
            return;
        }

        var subject = Substitute(template.Subject, tokens);
        var body = Substitute(template.BodyHtml, tokens);

        _dbContext.Notifications.Add(new Notification
        {
            RecipientUserId = user.Id,
            Channel = NotificationChannel.InApp,
            Subject = subject,
            Body = body,
            SentAtUtc = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _emailSender.SendEmailAsync(user.Email, subject, body, cancellationToken);
    }

    private static string Substitute(string template, IDictionary<string, string> tokens)
    {
        foreach (var (key, value) in tokens)
        {
            template = template.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
        }
        return template;
    }
}
