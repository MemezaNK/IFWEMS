namespace IFWEMS.Application.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, string DisplayName, IReadOnlyList<string> Roles);
