using Teta.Ippcms.Application.Projects;
using Teta.Ippcms.Application.Strategy;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Strategy;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.IntegrationTests;

/// <summary>API host + a cast of signed-in users (one per role), shared by all tests of a test class.</summary>
public sealed class ScenarioFixture : IDisposable
{
    public ScenarioFixture()
    {
        Factory = new TetaApiFactory();
        Cast = new Cast(Factory, "u" + Guid.NewGuid().ToString("N")[..6]);
    }

    public TetaApiFactory Factory { get; }
    public Cast Cast { get; }

    public void Dispose() => Factory.Dispose();
}

/// <summary>A cast of signed-in users (one per role).</summary>
public sealed class Cast
{
    private Guid? _indicatorId;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TetaApiFactory _factory;
    private readonly string _prefix;
    private readonly Dictionary<string, HttpClient> _clients = new();
    public Dictionary<string, Guid> UserIds { get; } = new();

    public Cast(TetaApiFactory factory, string prefix)
    {
        _factory = factory;
        _prefix = prefix;
    }

    public async Task<HttpClient> AsAsync(string role)
    {
        await _lock.WaitAsync();
        try
        {
            if (_clients.TryGetValue(role, out var client)) return client;
            var username = $"{_prefix}.{role}".ToLowerInvariant();
            UserIds[role] = _factory.CreateUser(username, role);
            client = await _factory.ClientForAsync(username);
            _clients[role] = client;
            return client;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>An APP indicator (plan → objective → indicator) that projects can be aligned to (FR-STR-004).</summary>
    public async Task<Guid> IndicatorAsync()
    {
        if (_indicatorId is { } existing) return existing;
        var strategy = await AsAsync(Roles.StrategyOfficer);
        var code = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var plan = await TetaApiFactory.PostAsync<PlanSummaryDto>(strategy, "/api/v1/strategy/plans",
            new SavePlanRequest("SP-" + code, "Strategic plan " + code, null, new DateOnly(2025, 4, 1), new DateOnly(2030, 3, 31)));
        var outcome = await TetaApiFactory.PostAsync<OutcomeDto>(strategy, $"/api/v1/strategy/plans/{plan.Id}/outcomes", new SaveOutcomeRequest("O1", "Skilled workforce"));
        var objective = await TetaApiFactory.PostAsync<ObjectiveDto>(strategy, $"/api/v1/strategy/plans/{plan.Id}/objectives",
            new SaveObjectiveRequest(outcome.Id, "SO1", "Increase learnerships", "COO", null, "Learnerships"));
        var indicator = await TetaApiFactory.PostAsync<IndicatorDto>(strategy, "/api/v1/strategy/indicators", new SaveIndicatorRequest(objective.Id, "PI1",
            "Learners entering learnerships", "Learners", "Sum of verified results", "Signed agreements", "Learner agreement", "COO", null, true));
        await TetaApiFactory.PostAsync<TargetDto>(strategy, "/api/v1/strategy/targets", new SaveTargetRequest(indicator.Id, "2026/27", 1, 100));
        _indicatorId = indicator.Id;
        return indicator.Id;
    }

    public Task<HttpClient> PmoAsync() => AsAsync(Roles.HeadPmo);
    public Task<HttpClient> PmAsync() => AsAsync(Roles.ProjectManager);
    public Task<HttpClient> CfoAsync() => AsAsync(Roles.Cfo);
    public Task<HttpClient> ExecAsync() => AsAsync(Roles.ExecutiveAuthority);

    /// <summary>Approves the pending step of a workflow instance as the given role.</summary>
    public async Task<DecisionResult> DecideAsync(string role, Guid instanceId, TaskDecision decision = TaskDecision.Approved, string? comment = null)
    {
        var client = await AsAsync(role);
        var inbox = await TetaApiFactory.GetAsync<List<InboxItemDto>>(client, "/api/v1/inbox");
        var item = inbox.SingleOrDefault(i => i.InstanceId == instanceId)
                   ?? throw new InvalidOperationException($"No inbox task for {role} on workflow {instanceId}. Inbox: {string.Join(", ", inbox.Select(i => i.StepName))}");
        return await TetaApiFactory.PostAsync<DecisionResult>(client, $"/api/v1/inbox/{item.TaskId}/decision",
            new { decision, comment = comment ?? $"Decision by {role}" });
    }

    /// <summary>Runs every remaining approval step using the role each task is assigned to.</summary>
    public async Task<WorkflowInstanceDto> ApproveAllAsync(Guid instanceId)
    {
        for (var guard = 0; guard < 10; guard++)
        {
            var any = await PmoAsync();
            var instance = await TetaApiFactory.GetAsync<WorkflowInstanceDto>(await ExecAsync(), $"/api/v1/workflows/{instanceId}");
            if (instance.State != "InProgress") return instance;
            var pending = instance.Tasks.Single(t => t.Decision == "Pending");
            await DecideAsync(pending.AssignedRole, instanceId);
            _ = any;
        }
        throw new InvalidOperationException("Workflow did not complete.");
    }

    /// <summary>Creates portfolio → programme → registered project (as HeadPMO / PM) and returns the project.</summary>
    public async Task<ProjectDetailDto> NewProjectAsync(string name, decimal? approvedBudget = null)
    {
        var pmo = await PmoAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var portfolio = await TetaApiFactory.PostAsync<PortfolioDto>(pmo, "/api/v1/portfolios", new SavePortfolioRequest("PF" + suffix, "Portfolio " + suffix, null, "Head PMO"));
        var programme = await TetaApiFactory.PostAsync<ProgrammeDto>(pmo, "/api/v1/programmes", new SaveProgrammeRequest(portfolio.Id, "PG" + suffix, "Programme " + suffix, null, null, "Owner"));
        var pm = await PmAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var project = await TetaApiFactory.PostAsync<ProjectDetailDto>(pm, "/api/v1/projects", new SaveProjectRequest(name, "Test project", programme.Id, null,
            "Sponsor", UserIds[Roles.ProjectManager], "PM", "Business owner", "Projects/PMO", "Learnership", "Gauteng", "Johannesburg", "City of Johannesburg",
            today, today.AddMonths(12)));
        await TetaApiFactory.PostAsync<IndicatorLinkDto>(pm, $"/api/v1/strategy/projects/{project.Id}/links",
            new LinkIndicatorRequest(await IndicatorAsync(), ContributionMethod.Direct, 1m, 100m));
        return project;
    }

    /// <summary>Takes a new project through business-case approval so it has a PRJ number and approved budget.</summary>
    public async Task<ProjectDetailDto> ApprovedProjectAsync(string name, decimal cost = 2_500_000m)
    {
        var project = await NewProjectAsync(name);
        var pm = await PmAsync();
        await TetaApiFactory.PutAsync<BusinessCaseDto>(pm, $"/api/v1/projects/{project.Id}/business-case", new SaveBusinessCaseRequest(
            "Youth unemployment in the sector", "Train 250 learners", "Option A vs B", "Learnership delivery", "Employed learners", cost,
            "Provider capacity", "Accredited providers", "Learners placed", 250));
        var wf = await TetaApiFactory.PostAsync<WorkflowInstanceDto>(pm, $"/api/v1/projects/{project.Id}/business-case/submit", null);
        await ApproveAllAsync(wf.Id);
        return await TetaApiFactory.GetAsync<ProjectDetailDto>(pm, $"/api/v1/projects/{project.Id}");
    }
}
