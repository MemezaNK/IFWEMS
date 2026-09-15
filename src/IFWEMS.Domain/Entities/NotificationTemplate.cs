namespace IFWEMS.Domain.Entities;

/// <summary>
/// In-app editable email/notification template. FR-005.
/// </summary>
public class NotificationTemplate : Common.BaseEntity
{
    public string Code { get; set; } = default!; // e.g. "SLA_WARNING", "CASE_ASSIGNED"
    public string Subject { get; set; } = default!;
    public string BodyHtml { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}

public enum NotificationChannel
{
    Email,
    InApp
}

/// <summary>
/// A notification instance sent/queued for a user. FR-006.
/// </summary>
public class Notification : Common.BaseEntity
{
    public Guid RecipientUserId { get; set; }
    public User RecipientUser { get; set; } = default!;
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public string Subject { get; set; } = default!;
    public string Body { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTime? SentAtUtc { get; set; }
}
