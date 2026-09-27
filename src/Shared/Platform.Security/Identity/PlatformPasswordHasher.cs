using Microsoft.AspNetCore.Identity;

namespace Platform.Security.Identity;

/// <summary>
/// Password hashing/verification compatible with IFWEMS (ASP.NET Core Identity PasswordHasher, V3
/// format: PBKDF2-HMAC-SHA256 with per-password salt). The hash format does not depend on the user
/// type, so hashes written by either application verify in both.
/// </summary>
public interface IPlatformPasswordHasher
{
    string Hash(string password);
    PasswordCheck Verify(string hash, string password);
}

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded
}

public sealed class PlatformPasswordHasher : IPlatformPasswordHasher
{
    private readonly PasswordHasher<PlatformUser> _inner = new();
    private static readonly PlatformUser Dummy = new();

    public string Hash(string password) => _inner.HashPassword(Dummy, password);

    public PasswordCheck Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password)) return PasswordCheck.Failed;
        try
        {
            return _inner.VerifyHashedPassword(Dummy, hash, password) switch
            {
                PasswordVerificationResult.Success => PasswordCheck.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
                _ => PasswordCheck.Failed
            };
        }
        catch (FormatException)
        {
            return PasswordCheck.Failed;
        }
    }
}

/// <summary>Minimum password rules applied when users set or reset a password.</summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 10;

    public static IReadOnlyList<string> Validate(string? password)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
            errors.Add($"Password must be at least {MinimumLength} characters.");
        if (password is not null)
        {
            if (!password.Any(char.IsUpper)) errors.Add("Password must contain an upper-case letter.");
            if (!password.Any(char.IsLower)) errors.Add("Password must contain a lower-case letter.");
            if (!password.Any(char.IsDigit)) errors.Add("Password must contain a digit.");
            if (password.All(char.IsLetterOrDigit)) errors.Add("Password must contain a symbol.");
        }
        return errors;
    }
}
