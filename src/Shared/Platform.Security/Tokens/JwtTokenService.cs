using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Platform.Security.Tokens;

/// <summary>
/// JWT settings for one application. Each application uses its own issuer/audience so a token
/// issued for IFWEMS is not accepted by TETA (and vice versa), even when they share a signing key.
/// </summary>
public sealed class JwtOptions
{
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string Key { get; set; } = default!;
    public int AccessTokenMinutes { get; set; } = 30;

    public static JwtOptions FromConfiguration(IConfiguration configuration, string sectionName = "Jwt")
    {
        var section = configuration.GetSection(sectionName);
        var options = new JwtOptions
        {
            Issuer = section["Issuer"] ?? throw new InvalidOperationException($"{sectionName}:Issuer configuration is missing."),
            Audience = section["Audience"] ?? throw new InvalidOperationException($"{sectionName}:Audience configuration is missing."),
            Key = section["Key"] ?? throw new InvalidOperationException($"{sectionName}:Key configuration is missing."),
            AccessTokenMinutes = int.TryParse(section["AccessTokenMinutes"], out var m) && m > 0 ? m : 30
        };
        if (Encoding.UTF8.GetByteCount(options.Key) < 32)
        {
            throw new InvalidOperationException($"{sectionName}:Key must be at least 32 bytes (256 bits) for HMAC-SHA256 signing.");
        }
        return options;
    }

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

/// <summary>Claims placed in platform access tokens.</summary>
public static class PlatformClaims
{
    public const string SessionId = "sid";
    public const string AuthMethod = "amr";
    public const string DisplayName = "display_name";
    public const string SupplierId = "supplier_id";
    public const string Permission = "permission";
}

public sealed record TokenRequest(
    Guid UserId,
    string Username,
    string DisplayName,
    IEnumerable<string> Roles,
    string SessionId,
    bool MfaSatisfied,
    IDictionary<string, string>? ExtraClaims = null);

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(TokenRequest request);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(JwtOptions options) => _options = options;

    public (string Token, DateTime ExpiresAtUtc) CreateToken(TokenRequest request)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, request.UserId.ToString()),
            new(ClaimTypes.Name, request.Username),
            new(PlatformClaims.DisplayName, request.DisplayName),
            new(JwtRegisteredClaimNames.Jti, request.SessionId),
            new(PlatformClaims.SessionId, request.SessionId),
            new(PlatformClaims.AuthMethod, request.MfaSatisfied ? "mfa" : "pwd")
        };
        claims.AddRange(request.Roles.Distinct().Select(r => new Claim(ClaimTypes.Role, r)));
        if (request.ExtraClaims is not null)
        {
            claims.AddRange(request.ExtraClaims.Select(kv => new Claim(kv.Key, kv.Value)));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-5),
            expires: expires,
            signingCredentials: new SigningCredentials(_options.SigningKey(), SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

public static class JwtBearerSetup
{
    /// <summary>Standard validation parameters for platform access tokens.</summary>
    public static TokenValidationParameters ValidationParameters(JwtOptions options) => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        IssuerSigningKey = options.SigningKey(),
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };

    public static void Configure(JwtBearerOptions bearer, JwtOptions options)
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = ValidationParameters(options);
    }
}
