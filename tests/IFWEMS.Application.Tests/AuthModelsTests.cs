using IFWEMS.Application.Auth;

namespace IFWEMS.Application.Tests;

public class AuthModelsTests
{
    [Fact]
    public void LoginResponse_ExposesRolesReadOnlyList()
    {
        var response = new LoginResponse("token123", DateTime.UtcNow.AddMinutes(30), "Jane Doe", new List<string> { "CaseOfficer" });

        Assert.Equal("Jane Doe", response.DisplayName);
        Assert.Contains("CaseOfficer", response.Roles);
    }
}
