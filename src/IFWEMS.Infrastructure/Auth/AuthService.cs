using IFWEMS.Application.Auth;
using IFWEMS.Application.Common.Interfaces;
using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Infrastructure.Auth;

public class AuthService : IAuthService
{
    // Account lockout policy (NFR 4.2 #13): lock the account for a cool-down period after
    // repeated failed attempts, to slow down credential-stuffing/brute-force attacks.
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IfwemsDbContext _dbContext;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(IfwemsDbContext dbContext, IJwtTokenGenerator tokenGenerator)
    {
        _dbContext = dbContext;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<LoginResponse?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Username == username && u.IsActive, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (user.LockedOutUntilUtc is { } lockedUntil && lockedUntil > DateTime.UtcNow)
        {
            return null;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockedOutUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        user.FailedLoginAttempts = 0;
        user.LockedOutUntilUtc = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(user.Id, user.Username, roles);

        return new LoginResponse(token, expiresAtUtc, user.DisplayName, roles);
    }
}
