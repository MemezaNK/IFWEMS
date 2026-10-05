using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Platform.Core;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>
/// Signs people in to the embedded VeriTrailX document processor. VeriTrailX shows no login of its own inside TETA: it trusts a
/// short-lived token signed here, with a secret only this server and VeriTrailX know, for the person who is signed in to TETA.
/// </summary>
[Route("api/v1/docproc")]
[HasPermission(Permissions.DocumentsRead)]
public sealed class DocProcEmbedController : TetaControllerBase
{
    /// <summary>VeriTrailX refuses any other audience.</summary>
    private const string Audience = "docproc";

    /// <summary>The token only starts a session, so it can be short.</summary>
    private const int TokenMinutes = 5;

    /// <summary>VeriTrailX refuses shorter secrets.</summary>
    private const int MinSecretLength = 32;

    private readonly ICurrentUser _user;
    private readonly IConfiguration _config;

    public DocProcEmbedController(ICurrentUser user, IConfiguration config)
    {
        _user = user;
        _config = config;
    }

    public sealed record DocProcEmbedTokenDto(string BaseUrl, string Token);

    /// <summary>The address of the document processor and a token for the signed-in person.</summary>
    [HttpGet("embed-token")]
    public ActionResult<DocProcEmbedTokenDto> GetEmbedToken()
    {
        // Suppliers use their own portal only; they have no place in the internal document processor.
        if (_user.IsInRole(Roles.Supplier) || _user.UserId is not { } userId) return Forbid();

        var baseUrl = (_config["DocProc:Url"] ?? string.Empty).Trim().TrimEnd('/');
        var secret = _config["DocProc:EmbedSecret"] ?? string.Empty;
        var hostId = (_config["DocProc:HostId"] ?? "teta").Trim();
        if (baseUrl.Length == 0 || secret.Length < MinSecretLength)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "Document processing is not set up on this server (DocProc:Url and DocProc:EmbedSecret).");

        return new DocProcEmbedTokenDto(baseUrl, CreateToken(userId, secret, hostId));
    }

    private string CreateToken(Guid userId, string secret, string hostId)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new JwtPayload
        {
            // This host's id: VeriTrailX uses it to pick this host's secret and project groups.
            { "iss", hostId },
            // The person's stable id in TETA. VeriTrailX keeps people of different hosts apart.
            { "sub", userId.ToString() },
            { "name", string.IsNullOrWhiteSpace(_user.DisplayName) ? _user.Username ?? userId.ToString() : _user.DisplayName },
            // "admin" sets things up in VeriTrailX (document types, checks, ...); anything else only checks and corrects.
            // (Administrators of a host are not administrators of VeriTrailX itself: which groups they reach is decided there.)
            { "role", IsAdministrator() ? "admin" : "reviewer" },
            { "aud", Audience },
            { "iat", now.ToUnixTimeSeconds() },
            { "exp", now.AddMinutes(TokenMinutes).ToUnixTimeSeconds() }
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));
    }

    /// <summary>
    /// People who may change how TETA is set up: those who configure or approve configuration changes (CFO, Head of SCM, ...),
    /// plus any role named in DocProc:AdminRoles. TETA's system administrators have no access to business documents by design,
    /// so they do not reach document processing at all (that needs documents.read).
    /// </summary>
    private bool IsAdministrator()
    {
        if (_user.HasPermission(Permissions.AdminConfig) || _user.HasPermission(Permissions.AdminConfigApprove)) return true;
        var extra = _config.GetSection("DocProc:AdminRoles").Get<string[]>() ?? Array.Empty<string>();
        return extra.Any(role => !string.IsNullOrWhiteSpace(role) && _user.IsInRole(role.Trim()));
    }
}
