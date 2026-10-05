using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.IntegrationTests;

/// <summary>The sign-in the embedded document processor trusts: a short-lived token signed for the person using TETA.</summary>
public sealed class DocProcEmbedTests : IClassFixture<TetaApiFactory>
{
    private const string Url = "/api/v1/docproc/embed-token";

    private readonly TetaApiFactory _factory;

    public DocProcEmbedTests(TetaApiFactory factory) => _factory = factory;

    private sealed record EmbedToken(string BaseUrl, string Token);

    /// <summary>Checks the token the way the document processor does: HS256, its own secret, audience "docproc".</summary>
    private static JwtSecurityToken Validate(string token, string secret)
    {
        new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateIssuer = false,
            ValidateAudience = true,
            ValidAudience = "docproc",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        }, out var validated);
        return (JwtSecurityToken)validated;
    }

    [Fact]
    public async Task Requires_a_signed_in_person()
    {
        var response = await _factory.CreateClient().GetAsync(Url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_project_manager_gets_a_reviewer_token_for_themselves()
    {
        var client = await _factory.NewUserClientAsync("docproc.pm", Roles.ProjectManager);
        var me = await TetaApiFactory.GetAsync<Teta.Ippcms.Application.Security.CurrentUserDto>(client, "/api/v1/auth/me");

        var result = await TetaApiFactory.GetAsync<EmbedToken>(client, Url);

        Assert.Equal("http://docproc.test:8000", result.BaseUrl); // the trailing slash is dropped
        var token = Validate(result.Token, TetaApiFactory.DocProcSecret);
        Assert.Equal("teta", token.Issuer); // which host this is: the document processor picks the host's secret and groups by it
        Assert.Equal(me.UserId.ToString(), token.Subject);
        Assert.Equal("reviewer", token.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal("docproc.pm", token.Claims.Single(c => c.Type == "name").Value);
        var lifetime = token.ValidTo - DateTime.UtcNow;
        Assert.InRange(lifetime.TotalMinutes, 3, 6); // only starts a session: short
    }

    [Theory]
    [InlineData(Roles.Cfo)]
    [InlineData(Roles.HeadScm)]
    public async Task People_who_approve_configuration_changes_get_an_admin_token(string role)
    {
        var client = await _factory.NewUserClientAsync("docproc.admin." + role.ToLowerInvariant(), role);
        var result = await TetaApiFactory.GetAsync<EmbedToken>(client, Url);
        Assert.Equal("admin", Validate(result.Token, TetaApiFactory.DocProcSecret).Claims.Single(c => c.Type == "role").Value);
    }

    [Fact]
    public async Task A_role_named_in_the_settings_is_an_administrator_too()
    {
        var signedIn = await _factory.NewUserClientAsync("docproc.headpmo", Roles.HeadPmo);
        var token = signedIn.DefaultRequestHeaders.Authorization!.Parameter!;
        Assert.Equal("reviewer", await RoleInTokenAsync(_factory, token)); // not an administrator by default

        using var named = _factory.WithWebHostBuilder(b => b.UseSetting("DocProc:AdminRoles:0", Roles.HeadPmo));
        Assert.Equal("admin", await RoleInTokenAsync(named, token));
    }

    private static async Task<string> RoleInTokenAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, string accessToken)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var result = await TetaApiFactory.GetAsync<EmbedToken>(client, Url);
        return Validate(result.Token, TetaApiFactory.DocProcSecret).Claims.Single(c => c.Type == "role").Value;
    }

    [Fact]
    public async Task System_administrators_have_no_business_document_access_and_so_none_here()
    {
        var client = await _factory.NewUserClientAsync("docproc.sysadmin", Roles.SystemAdministrator);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task The_token_is_not_valid_with_any_other_secret()
    {
        var client = await _factory.NewUserClientAsync("docproc.other", Roles.ProjectManager);
        var result = await TetaApiFactory.GetAsync<EmbedToken>(client, Url);
        Assert.ThrowsAny<SecurityTokenException>(() => Validate(result.Token, "another-secret-another-secret-0123456789"));
    }

    [Fact]
    public async Task Suppliers_have_no_access()
    {
        var client = await _factory.NewUserClientAsync("docproc.supplier", Roles.Supplier);
        var response = await client.GetAsync(Url);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Says_so_when_the_document_processor_is_not_set_up()
    {
        var signedIn = await _factory.NewUserClientAsync("docproc.unconfigured", Roles.ProjectManager);
        var token = signedIn.DefaultRequestHeaders.Authorization!.Parameter!;

        using var unconfigured = _factory.WithWebHostBuilder(b => b.UseSetting("DocProc:EmbedSecret", ""));
        var client = unconfigured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(Url);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("DocProc:EmbedSecret", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_secret_that_is_too_short_is_treated_as_not_set_up()
    {
        var signedIn = await _factory.NewUserClientAsync("docproc.short", Roles.ProjectManager);
        var token = signedIn.DefaultRequestHeaders.Authorization!.Parameter!;

        using var weak = _factory.WithWebHostBuilder(b => b.UseSetting("DocProc:EmbedSecret", "too-short"));
        var client = weak.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(Url)).StatusCode);
    }
}
