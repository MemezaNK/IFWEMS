using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Teta.Ippcms.Application.Execution;
using Teta.Ippcms.Application.Finance;
using Teta.Ippcms.Application.Jobs;
using Teta.Ippcms.Application.Reporting;
using Teta.Ippcms.Application.Workflow;

namespace Teta.Ippcms.Infrastructure.Jobs;

/// <summary>
/// In-process scheduler for TETA background jobs (SRS §8.3). Each job runs in its own DI scope as
/// the "system" user; failures are logged and retried on the next tick. Disable with
/// <c>Jobs:Enabled=false</c> (e.g. on secondary web nodes or in tests).
/// </summary>
public sealed class TetaJobHost : BackgroundService
{
    private sealed record Job(string Name, TimeSpan Interval, Func<IServiceProvider, CancellationToken, Task<int>> Run)
    {
        public DateTime NextRunUtc { get; set; }
    }

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TetaJobHost> _logger;
    private readonly bool _enabled;
    private readonly List<Job> _jobs;

    public TetaJobHost(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<TetaJobHost> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _enabled = configuration.GetValue("Jobs:Enabled", true);
        _jobs = new List<Job>
        {
            new("outbox-email", TimeSpan.FromSeconds(30), (sp, ct) => sp.GetRequiredService<IScheduledJobs>().DispatchOutboxAsync(ct)),
            new("workflow-escalation", TimeSpan.FromMinutes(15), (sp, ct) => sp.GetRequiredService<IWorkflowService>().EscalateOverdueAsync(ct)),
            new("overdue-escalation", TimeSpan.FromHours(1), (sp, ct) => sp.GetRequiredService<IScheduledJobs>().EscalateOverdueItemsAsync(ct)),
            new("scheduled-reports", TimeSpan.FromMinutes(15), (sp, ct) => sp.GetRequiredService<IReportingService>().RunDueSchedulesAsync(ct)),
            new("contract-expiry", TimeSpan.FromHours(6), (sp, ct) => sp.GetRequiredService<IScheduledJobs>().ContractExpiryAlertsAsync(ct)),
            new("document-expiry", TimeSpan.FromHours(6), (sp, ct) => sp.GetRequiredService<IScheduledJobs>().DocumentExpiryAlertsAsync(ct)),
            new("milestone-overdue", TimeSpan.FromHours(6), (sp, ct) => sp.GetRequiredService<IScheduledJobs>().MilestoneOverdueAlertsAsync(ct)),
            new("health-recalculation", TimeSpan.FromHours(12), (sp, ct) => sp.GetRequiredService<IExecutionService>().RecalculateAllHealthAsync(ct)),
            new("data-quality-scan", TimeSpan.FromHours(12), (sp, ct) => sp.GetRequiredService<IReportingService>().ScanDataQualityAsync(ct)),
            new("erp-retry", TimeSpan.FromHours(1), async (sp, ct) => (await sp.GetRequiredService<IFinanceService>().RetryErpErrorsAsync(ct)).Processed),
            new("app-evidence", TimeSpan.FromDays(7), (sp, ct) => sp.GetRequiredService<IScheduledJobs>().AppEvidenceCompletenessAsync(ct)),
            new("retention-review", TimeSpan.FromDays(7), (sp, ct) => sp.GetRequiredService<IScheduledJobs>().RetentionReviewAsync(ct)),
            new("session-housekeeping", TimeSpan.FromDays(1), (sp, ct) => sp.GetRequiredService<IScheduledJobs>().SessionHousekeepingAsync(ct))
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("TETA background jobs are disabled (Jobs:Enabled=false)");
            return;
        }

        // Stagger first runs so application start-up is not slowed down.
        var start = DateTime.UtcNow.AddSeconds(20);
        for (var i = 0; i < _jobs.Count; i++) _jobs[i].NextRunUtc = start.AddSeconds(i * 5);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var job in _jobs.Where(j => j.NextRunUtc <= DateTime.UtcNow))
            {
                job.NextRunUtc = DateTime.UtcNow.Add(job.Interval);
                await RunAsync(job, stoppingToken);
            }
        }
    }

    private async Task RunAsync(Job job, CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var count = await job.Run(scope.ServiceProvider, ct);
            if (count > 0) _logger.LogInformation("Job {Job} processed {Count} item(s)", job.Name, count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {Job} failed; it will be retried at {Next:u}", job.Name, job.NextRunUtc);
        }
    }
}
