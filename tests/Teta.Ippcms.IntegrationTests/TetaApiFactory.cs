using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Security.Identity;
using Platform.Security.Mfa;
using Teta.Ippcms.Application.Security;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Infrastructure.Persistence;

namespace Teta.Ippcms.IntegrationTests;

/// <summary>
/// Hosts the real TETA API in-memory against a private SQLite database built from the EF model
/// (the shared dbo.Users table, owned by IFWEMS, is created the same way IFWEMS would).
/// </summary>
public sealed class TetaApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Test!Passw0rd#2026";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly SqliteConnection _connection;
    private readonly string _documentsRoot = Path.Combine(Path.GetTempPath(), "teta-tests-" + Guid.NewGuid().ToString("N"));

    public TetaApiFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw(@"CREATE TABLE ""Users"" (
            ""Id"" TEXT NOT NULL PRIMARY KEY, ""Username"" TEXT NOT NULL UNIQUE, ""Email"" TEXT NOT NULL UNIQUE, ""DisplayName"" TEXT NOT NULL,
            ""PasswordHash"" TEXT NOT NULL, ""IsActive"" INTEGER NOT NULL, ""MfaEnabled"" INTEGER NOT NULL, ""FailedLoginAttempts"" INTEGER NOT NULL,
            ""LockedOutUntilUtc"" TEXT NULL, ""OrgUnitId"" TEXT NULL, ""CreatedAtUtc"" TEXT NOT NULL, ""CreatedBy"" TEXT NULL,
            ""ModifiedAtUtc"" TEXT NULL, ""ModifiedBy"" TEXT NULL)");
    }

    public TetaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TetaDbContext>().UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.SqliteEventId.SchemaConfiguredWarning)).Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=not-used-in-tests");
        builder.UseSetting("Jwt:Issuer", "TETA-IPPCMS");
        builder.UseSetting("Jwt:Audience", "TETA-IPPCMS.Client");
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("Security:AuditSealKey", "integration-test-audit-seal-key-0123456789");
        builder.UseSetting("Security:FieldEncryptionKey", "integration-test-field-encryption-key-0123");
        builder.UseSetting("Integration:ErpApiKey", "integration-test-erp-api-key-0123456789abcdef");
        builder.UseSetting("HealthChecks:SqlServer", "false");
        builder.UseSetting("Jobs:Enabled", "false");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Teta:BootstrapAdmins", "");
        builder.UseSetting("UseHttpsRedirection", "false");
        builder.UseSetting("RateLimiting:PerMinute", "100000");
        builder.UseSetting("RateLimiting:LoginPerMinute", "100000");
        builder.UseSetting("Documents:RootPath", _documentsRoot);
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");

        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType == typeof(DbContextOptions<TetaDbContext>) || d.ServiceType == typeof(DbContextOptions)).ToList())
                services.Remove(d);
            services.AddDbContext<TetaDbContext>((sp, o) =>
                o.UseSqlite(_connection).ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.SqliteEventId.SchemaConfiguredWarning))
                    .AddInterceptors(sp.GetRequiredService<TetaSaveChangesInterceptor>()));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            try { Directory.Delete(_documentsRoot, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    // ---------- Test data helpers ----------

    /// <summary>Creates a shared-platform user with active TETA role assignments (optionally project-scoped).</summary>
    public Guid CreateUser(string username, params string[] roles) => CreateUser(username, ScopeType.Global, null, roles);

    public Guid CreateUser(string username, ScopeType scope, Guid? scopeId, params string[] roles)
    {
        using var db = CreateContext();
        var user = new PlatformUser
        {
            Username = username, Email = username + "@test.local", DisplayName = username, PasswordHash = new PlatformPasswordHasher().Hash(Password),
            IsActive = true, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "test"
        };
        db.Users.Add(user);
        foreach (var role in roles)
        {
            db.UserRoleAssignments.Add(new UserRoleAssignment
            {
                UserId = user.Id, RoleCode = role, ScopeType = scope, ScopeId = scopeId, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
                Status = AssignmentStatus.Active, ApprovedBy = "test", RequestedBy = "test"
            });
        }
        db.SaveChanges();
        return user.Id;
    }

    /// <summary>Signs in through the API (completing TOTP enrolment/verification for privileged roles) and returns an authenticated client.</summary>
    public async Task<HttpClient> ClientForAsync(string username)
    {
        var client = CreateClient();
        var login = await PostAsync<LoginResult>(client, "/api/v1/auth/login", new LoginRequest(username, Password));
        Assert.True(login.Succeeded, login.Message);
        if (login.MfaEnrolmentRequired)
        {
            var enrol = await PostAsync<MfaEnrolmentDto>(client, "/api/v1/auth/mfa/enrol", new { challengeToken = login.ChallengeToken });
            var code = new TotpService().ComputeCode(enrol.Secret, DateTime.UtcNow);
            login = await PostAsync<LoginResult>(client, "/api/v1/auth/mfa/enrol/confirm", new MfaConfirmRequest(enrol.ChallengeToken, code));
        }
        else if (login.MfaRequired)
        {
            throw new InvalidOperationException("Re-login of an MFA-enrolled test user is not supported by this helper.");
        }
        Assert.True(login.Succeeded, login.Message);
        Assert.False(string.IsNullOrEmpty(login.AccessToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    public async Task<HttpClient> NewUserClientAsync(string username, params string[] roles)
    {
        CreateUser(username, roles);
        return await ClientForAsync(username);
    }

    public static async Task<T> PostAsync<T>(HttpClient client, string url, object? body)
    {
        var response = await client.PostAsJsonAsync(url, body, Json);
        return await ReadAsync<T>(response);
    }

    public static async Task<T> PutAsync<T>(HttpClient client, string url, object? body)
    {
        var response = await client.PutAsJsonAsync(url, body, Json);
        return await ReadAsync<T>(response);
    }

    public static async Task<T> GetAsync<T>(HttpClient client, string url) => await ReadAsync<T>(await client.GetAsync(url));

    /// <summary>Uploads a small evidence file through the documents API.</summary>
    public static async Task<Teta.Ippcms.Application.Documents.EvidenceDto> UploadEvidenceAsync(HttpClient client, string parentType, Guid parentId, string evidenceType)
    {
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 evidence " + Guid.NewGuid())), "File", "evidence.pdf" },
            { new StringContent(parentType), "ParentType" },
            { new StringContent(parentId.ToString()), "ParentId" },
            { new StringContent(evidenceType), "DocumentType" },
            { new StringContent(evidenceType), "EvidenceType" },
            { new StringContent("Internal"), "Classification" }
        };
        return await ReadAsync<Teta.Ippcms.Application.Documents.EvidenceDto>(await client.PostAsync("/api/v1/documents/evidence", form));
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)response.StatusCode} {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {text}", null, response.StatusCode);
        return JsonSerializer.Deserialize<T>(text, Json)!;
    }
}
