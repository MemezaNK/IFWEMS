using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Platform.Security.Tokens;

namespace Teta.Ippcms.Api.Security;

/// <summary>
/// Requires the caller to hold at least one of the listed permissions (SEC-003). Permissions are
/// resolved server-side from the user's effective TETA role assignments on every request.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string Prefix = "perm:";

    public HasPermissionAttribute(params string[] permissions) : base(Prefix + string.Join('|', permissions))
    {
    }
}

/// <summary>Builds "perm:a|b" policies on demand (any-of semantics).</summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(HasPermissionAttribute.Prefix, StringComparison.Ordinal)) return await base.GetPolicyAsync(policyName);
        var permissions = policyName[HasPermissionAttribute.Prefix.Length..].Split('|', StringSplitOptions.RemoveEmptyEntries);
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => permissions.Any(p => ctx.User.HasClaim(PlatformClaims.Permission, p)))
            .Build();
    }
}
