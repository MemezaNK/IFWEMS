namespace IFWEMS.Domain.Entities;

/// <summary>
/// Immutable audit record. No application-level delete/update capability. FR-051.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? SessionId { get; set; }
    public string Action { get; set; } = default!;
    public string EntityName { get; set; } = default!;
    public string? EntityId { get; set; }
    public string? BeforeValuesJson { get; set; }
    public string? AfterValuesJson { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
