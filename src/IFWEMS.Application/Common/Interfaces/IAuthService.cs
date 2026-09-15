using IFWEMS.Application.Auth;

namespace IFWEMS.Application.Common.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
}

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(Guid userId, string username, IEnumerable<string> roles);
}
