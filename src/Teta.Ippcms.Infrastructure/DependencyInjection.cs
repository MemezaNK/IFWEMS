using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Audit;
using Platform.Core;
using Platform.Notifications;
using Platform.Security.Crypto;
using Platform.Security.Identity;
using Platform.Security.Mfa;
using Platform.Security.Tokens;
using Platform.Security.Web;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Admin;
using Teta.Ippcms.Application.Assurance;
using Teta.Ippcms.Application.Audit;
using Teta.Ippcms.Application.Budget;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.ContractManagement;
using Teta.Ippcms.Application.Documents;
using Teta.Ippcms.Application.Execution;
using Teta.Ippcms.Application.Finance;
using Teta.Ippcms.Application.Home;
using Teta.Ippcms.Application.Jobs;
using Teta.Ippcms.Application.Monitoring;
using Teta.Ippcms.Application.Portal;
using Teta.Ippcms.Application.Projects;
using Teta.Ippcms.Application.Reporting;
using Teta.Ippcms.Application.Search;
using Teta.Ippcms.Application.Security;
using Teta.Ippcms.Application.Sourcing;
using Teta.Ippcms.Application.Strategy;
using Teta.Ippcms.Application.Suppliers;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Infrastructure.Jobs;
using Teta.Ippcms.Infrastructure.Persistence;
using Teta.Ippcms.Infrastructure.Seed;
using Teta.Ippcms.Infrastructure.Services;

namespace Teta.Ippcms.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers TETA persistence, shared platform services (identity, tokens, MFA, field
    /// encryption, audit sealing, e-mail), all application services, workflow completion handlers
    /// and background jobs.
    /// </summary>
    public static IServiceCollection AddTetaInfrastructure(this IServiceCollection services, IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDatabase = null)
    {
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();

        // ----- Shared platform services -----
        services.AddScoped<ICurrentUser>(sp =>
        {
            var accessor = sp.GetRequiredService<IHttpContextAccessor>();
            return accessor.HttpContext is null ? SystemUser.Instance : new HttpCurrentUser(accessor);
        });
        services.AddSingleton(JwtOptions.FromConfiguration(configuration, "Jwt"));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPlatformPasswordHasher, PlatformPasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IFieldProtector>(_ => AesGcmFieldProtector.FromConfiguration(configuration));
        services.AddSingleton(_ => new AuditSealer(RequireSecret(configuration, "Security:AuditSealKey")));
        services.AddSingleton(SmtpOptions.FromConfiguration(configuration));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        // ----- Persistence -----
        services.AddScoped<TetaSaveChangesInterceptor>();
        services.AddDbContext<TetaDbContext>((sp, options) =>
        {
            if (configureDatabase is not null)
            {
                configureDatabase(options);
            }
            else
            {
                var connection = configuration.GetConnectionString("DefaultConnection")
                                 ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");
                options.UseSqlServer(connection, sql =>
                {
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", TetaDbContext.Schema);
                    sql.CommandTimeout(60);
                });
            }
            options.AddInterceptors(sp.GetRequiredService<TetaSaveChangesInterceptor>());
        });
        services.AddScoped<ITetaDbContext>(sp => sp.GetRequiredService<TetaDbContext>());

        // ----- Infrastructure adapters -----
        services.AddScoped<INumberGenerator, NumberGenerator>();
        services.AddSingleton<IDocumentStorage, FileSystemDocumentStorage>();
        services.AddSingleton<IUploadScanner, DefaultMalwareScanner>();
        services.AddScoped<IErpGateway, QueuedErpGateway>();
        services.AddScoped<TetaSeeder>();

        // ----- Cross-cutting application services -----
        services.AddScoped<ISettings, SettingsService>();
        services.AddScoped<IAccessScope, AccessScopeService>();
        services.AddScoped<INotifier, Notifier>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IUserRoles, UserRolesService>();
        services.AddScoped<IDelegationService, DelegationService>();
        services.AddScoped<ITransactionLedger, TransactionLedger>();
        services.AddScoped<ISodService, SodService>();
        services.AddScoped<IWorkflowService, WorkflowService>();

        // ----- Module services -----
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IStrategyService, StrategyService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IGateChecks, GateChecks>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IProcurementService, ProcurementService>();
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<IExecutionService, ExecutionService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IMonitoringService, MonitoringService>();
        services.AddScoped<IRiskService, RiskService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<ILearnerDeliveryReportService, LearnerDeliveryReportService>();
        services.AddScoped<ILearnerDeliveryReportService, LearnerDeliveryReportService>();
        services.AddScoped<IHomeService, HomeService>();
        services.AddScoped<IPortalService, PortalService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IScheduledJobs, ScheduledJobs>();

        // ----- Workflow completion handlers (one per workflow entity type) -----
        services.AddScoped<IWorkflowCompletionHandler, BusinessCaseApprovalHandler>();
        services.AddScoped<IWorkflowCompletionHandler, StrategicPlanApprovalHandler>();
        services.AddScoped<IWorkflowCompletionHandler, AppTargetApprovalHandler>();
        services.AddScoped<IWorkflowCompletionHandler, RequisitionApprovalHandler>();
        services.AddScoped<IWorkflowCompletionHandler, AdjudicationApprovalHandler>();
        services.AddScoped<IWorkflowCompletionHandler, ProcurementExceptionApprovalHandler>();
        services.AddScoped<IWorkflowCompletionHandler, ContractVariationApprovalHandler>();
        services.AddScoped<IWorkflowCompletionHandler, ChangeRequestApprovalHandler>();
        services.AddScoped<IWorkflowCompletionHandler, ProjectClosureApprovalHandler>();
        services.AddScoped<IWorkflowCompletionHandler, InvoiceCertificationHandler>();

        services.AddHostedService<TetaJobHost>();
        return services;
    }

    /// <summary>Applies the idempotent configuration seed (and optional demo data) at start-up.</summary>
    public static async Task SeedTetaAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Teta.Seed");
        try
        {
            await scope.ServiceProvider.GetRequiredService<TetaSeeder>().SeedAsync(ct);
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or Microsoft.Data.SqlClient.SqlException)
        {
            // Never block start-up (health checks will report the database problem); log for operators.
            logger.LogError(ex, "TETA configuration seed failed");
        }
    }

    private static string RequireSecret(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase) || value.Length < 32)
            throw new InvalidOperationException($"{key} must be configured with a secret of at least 32 characters (environment variable or secret store).");
        return value;
    }
}
