using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Platform.Security.Tokens;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Api.Security;

/// <summary>
/// Machine-to-machine authentication for the ERP interface (FR-FIN-002, SRS §29): the caller sends
/// <c>X-Api-Key</c>; a match against <c>Integration:ErpApiKey</c> authenticates a system principal
/// holding only the <c>finance.erp</c> permission. Disabled when no key is configured.
/// </summary>
public static class ErpApiKeyAuthentication
{
    public const string Scheme = "ErpApiKey";
    public const string Header = "X-Api-Key";
}

public sealed class ErpApiKeyHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public ErpApiKeyHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configured = _configuration["Integration:ErpApiKey"];
        if (string.IsNullOrWhiteSpace(configured) || configured.Length < 32) return Task.FromResult(AuthenticateResult.NoResult());
        if (!Request.Headers.TryGetValue(ErpApiKeyAuthentication.Header, out var supplied) || string.IsNullOrEmpty(supplied))
            return Task.FromResult(AuthenticateResult.NoResult());

        var a = Encoding.UTF8.GetBytes(configured);
        var b = Encoding.UTF8.GetBytes(supplied.ToString());
        if (a.Length != b.Length || !CryptographicOperations.FixedTimeEquals(a, b))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()),
            new Claim(ClaimTypes.Name, "erp-interface"),
            new Claim(PlatformClaims.DisplayName, "ERP interface"),
            new Claim(PlatformClaims.Permission, Permissions.FinanceErp)
        }, ErpApiKeyAuthentication.Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), ErpApiKeyAuthentication.Scheme)));
    }
}
