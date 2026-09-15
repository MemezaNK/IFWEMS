using IFWEMS.Application.Common.Interfaces;
using IFWEMS.Infrastructure.Auth;
using IFWEMS.Infrastructure.Cases;
using IFWEMS.Infrastructure.Compliance;
using IFWEMS.Infrastructure.Notifications;
using IFWEMS.Infrastructure.Persistence;
using IFWEMS.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IFWEMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        services.AddSingleton<AppendOnlyAuditInterceptor>();
        services.AddScoped<AuditLoggingInterceptor>();

        services.AddDbContext<IfwemsDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            options.AddInterceptors(
                sp.GetRequiredService<AppendOnlyAuditInterceptor>(),
                sp.GetRequiredService<AuditLoggingInterceptor>());
        });

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ICaseNumberGenerator, CaseNumberGenerator>();
        services.AddScoped<ICaseService, CaseService>();
        services.AddScoped<IComplianceRuleEngine, ComplianceRuleEngine>();
        services.AddScoped<IEmergencyOverrideService, EmergencyOverrideService>();

        return services;
    }
}
