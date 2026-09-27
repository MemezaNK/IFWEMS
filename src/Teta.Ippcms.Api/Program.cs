using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Platform.Core.Web;
using Platform.Security.Tokens;
using Serilog;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Api.Web;
using Teta.Ippcms.Application.Security;
using Teta.Ippcms.Infrastructure;
using Teta.Ippcms.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "TETA-IPPCMS")
    .WriteTo.Console());

// ----- Application services -----
builder.Services.AddTetaInfrastructure(builder.Configuration);
builder.Services.AddPlatformProblemDetails("https://teta.local/problems/");
builder.Services.AddScoped<IdempotencyFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<IdempotencyFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });

// ----- Authentication: JWT (users) + API key (ERP interface) -----
var jwt = JwtOptions.FromConfiguration(builder.Configuration, "Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        JwtBearerSetup.Configure(options, jwt);
        options.Events = new JwtBearerEvents
        {
            // Server-side session check: revoked / inactive sessions are rejected immediately (SEC-009).
            OnTokenValidated = async ctx =>
            {
                var principal = ctx.Principal!;
                var sid = principal.FindFirstValue(PlatformClaims.SessionId);
                if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || string.IsNullOrEmpty(sid))
                {
                    ctx.Fail("Invalid token.");
                    return;
                }
                var auth = ctx.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                if (!await auth.ValidateSessionAsync(userId, sid, ctx.HttpContext.RequestAborted))
                    ctx.Fail("The session has expired or was revoked.");
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ErpApiKeyHandler>(ErpApiKeyAuthentication.Scheme, _ => { });
builder.Services.AddScoped<IClaimsTransformation, TetaClaimsTransformation>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme, ErpApiKeyAuthentication.Scheme)
        .RequireAuthenticatedUser().Build();
    options.FallbackPolicy = options.DefaultPolicy;
});

// ----- Rate limiting (abuse / credential stuffing protection) -----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimiting:PerMinute", 300), Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("login", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPerMinute", 10), Window = TimeSpan.FromMinutes(1) }));
});

// ----- Health checks -----
var healthChecks = builder.Services.AddHealthChecks();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString) && builder.Configuration.GetValue("HealthChecks:SqlServer", true))
    healthChecks.AddSqlServer(connectionString, name: "sqlserver", tags: new[] { "ready" });

builder.Services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 60 * 1024 * 1024);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "TETA-IPPCMS API", Version = "v1" });
    options.CustomSchemaIds(t => t.FullName?.Replace('+', '.'));
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT bearer token", Name = "Authorization", In = ParameterLocation.Header, Type = SecuritySchemeType.Http, Scheme = "bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<TetaDbContext>().Database.MigrateAsync();
}
if (app.Configuration.GetValue("Seed:Enabled", true))
{
    await app.Services.SeedTetaAsync();
}

// ----- Pipeline -----
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseCorrelationId();
app.UseSecurityHeaders();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("v1/swagger.json", "TETA-IPPCMS API v1"));
}
if (app.Configuration.GetValue("UseHttpsRedirection", true)) app.UseHttpsRedirection();

// Angular SPA (built with base href /TETA/) served from wwwroot on the same origin as the API.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();
app.Map("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Unknown API endpoint.")).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public partial class Program { }
