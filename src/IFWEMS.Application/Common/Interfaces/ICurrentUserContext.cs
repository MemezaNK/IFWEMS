namespace IFWEMS.Application.Common.Interfaces;

/// <summary>
/// Provides the identity/session context of the current request for audit logging purposes.
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    string? Username { get; }
    string? SessionId { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}
