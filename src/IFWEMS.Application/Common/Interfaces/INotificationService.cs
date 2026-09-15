namespace IFWEMS.Application.Common.Interfaces;

/// <summary>
/// Sends notifications (email + in-app) using configurable, in-app-editable templates. FR-005/FR-006.
/// Concrete SMTP delivery is a pluggable Infrastructure concern.
/// </summary>
public interface INotificationService
{
    Task SendAsync(Guid recipientUserId, string templateCode, IDictionary<string, string> tokens, CancellationToken cancellationToken = default);
}

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken cancellationToken = default);
}
