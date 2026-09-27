namespace Platform.Core;

/// <summary>
/// The authenticated caller of the current request (or a background job acting as "system").
/// Implemented per host so domain/application code never touches HttpContext directly.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Username { get; }
    string? DisplayName { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    string? CorrelationId { get; }
    string? SessionId { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    bool HasPermission(string permission);
}

/// <summary>Abstracted clock so time-dependent rules (expiry, SLA, delegation effective dates) are testable.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}

/// <summary>A fixed/advanceable clock for tests and simulations.</summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    public DateTime UtcNow { get; private set; }
    public DateOnly Today => DateOnly.FromDateTime(UtcNow);
    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    public void Set(DateTime utcNow) => UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
}

/// <summary>Identity used for work performed by scheduled jobs rather than a signed-in user.</summary>
public sealed class SystemUser : ICurrentUser
{
    public static readonly SystemUser Instance = new();
    public Guid? UserId => null;
    public string? Username => "system";
    public string? DisplayName => "System";
    public IReadOnlyCollection<string> Roles => Array.Empty<string>();
    public IReadOnlyCollection<string> Permissions => Array.Empty<string>();
    public string? IpAddress => null;
    public string? UserAgent => null;
    public string? CorrelationId => null;
    public string? SessionId => null;
    public bool IsAuthenticated => false;
    public bool IsInRole(string role) => false;
    public bool HasPermission(string permission) => false;
}
