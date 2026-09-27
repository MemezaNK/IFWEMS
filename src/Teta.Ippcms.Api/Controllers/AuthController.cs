using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Teta.Ippcms.Application.Security;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Sign-in with the shared platform credentials, TOTP MFA, session refresh and logout (SEC-001/002/009).</summary>
[Route("api/v1/auth")]
public sealed class AuthController : TetaControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    public sealed record BeginEnrolmentRequest(string? ChallengeToken);

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public Task<LoginResult> Login(LoginRequest request, CancellationToken ct) => _auth.LoginAsync(request, ClientIp, UserAgent, ct);

    [HttpPost("mfa/verify"), AllowAnonymous, EnableRateLimiting("login")]
    public Task<LoginResult> VerifyMfa(MfaVerifyRequest request, CancellationToken ct) => _auth.VerifyMfaAsync(request, ClientIp, UserAgent, ct);

    [HttpPost("mfa/enrol"), AllowAnonymous, EnableRateLimiting("login")]
    public Task<MfaEnrolmentDto> BeginEnrolment(BeginEnrolmentRequest request, CancellationToken ct) => _auth.BeginMfaEnrolmentAsync(request.ChallengeToken, ct);

    [HttpPost("mfa/enrol/confirm"), AllowAnonymous, EnableRateLimiting("login")]
    public Task<LoginResult> ConfirmEnrolment(MfaConfirmRequest request, CancellationToken ct) => _auth.ConfirmMfaEnrolmentAsync(request, ClientIp, UserAgent, ct);

    [HttpPost("refresh")]
    public Task<LoginResult> Refresh(CancellationToken ct) => _auth.RefreshAsync(ct);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _auth.LogoutAsync(ct);
        return NoContent();
    }

    [HttpGet("me")]
    public Task<CurrentUserDto> Me(CancellationToken ct) => _auth.MeAsync(ct);

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(request, ct);
        return NoContent();
    }
}
