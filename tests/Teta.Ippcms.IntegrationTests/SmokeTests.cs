using System.Net.Http.Headers;
using Teta.Ippcms.Application.Security;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.IntegrationTests;

public sealed class SmokeTests : IClassFixture<TetaApiFactory>
{
    private readonly TetaApiFactory _factory;

    public SmokeTests(TetaApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_endpoint_is_anonymous()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Api_requires_authentication()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/home");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Project_manager_can_sign_in_and_load_home()
    {
        var client = await _factory.NewUserClientAsync("smoke.pm", Roles.ProjectManager);
        var me = await TetaApiFactory.GetAsync<CurrentUserDto>(client, "/api/v1/auth/me");
        Assert.Contains(Roles.ProjectManager, me.Roles);
        Assert.Contains(Permissions.ProjectCreate, me.Permissions);
        var home = await client.GetAsync("/api/v1/home");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
    }
}
