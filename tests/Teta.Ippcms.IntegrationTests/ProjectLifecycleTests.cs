using Teta.Ippcms.Application.Projects;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Workflow;

namespace Teta.Ippcms.IntegrationTests;

/// <summary>UAT: project intake, business case approval workflow, project numbering, SoD and delegation (FR-POR, BR-002, BR-007, BR-008).</summary>
public sealed class ProjectLifecycleTests : IClassFixture<ScenarioFixture>
{
    private readonly TetaApiFactory _factory;
    private readonly Cast _cast;

    public ProjectLifecycleTests(ScenarioFixture fixture)
    {
        _factory = fixture.Factory;
        _cast = fixture.Cast;
    }

    [Fact]
    public async Task Business_case_approval_assigns_project_number_and_budget()
    {
        var project = await _cast.NewProjectAsync("Learnership intake 2026");
        Assert.StartsWith("CON-", project.Reference);
        Assert.Null(project.ProjectNumber);

        var pm = await _cast.PmAsync();
        // Incomplete business case cannot be submitted.
        await TetaApiFactory.PutAsync<BusinessCaseDto>(pm, $"/api/v1/projects/{project.Id}/business-case",
            new SaveBusinessCaseRequest("Problem", null, null, null, null, 0, null, null, null, null));
        var incomplete = await pm.PostAsync($"/api/v1/projects/{project.Id}/business-case/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode); // missing mandatory fields are reported per field

        await TetaApiFactory.PutAsync<BusinessCaseDto>(pm, $"/api/v1/projects/{project.Id}/business-case", new SaveBusinessCaseRequest(
            "Youth unemployment", "Train learners", "Options", "Scope", "Benefits", 3_000_000m, "Risks", "Delivery model", "Placement rate", 70));
        var wf = await TetaApiFactory.PostAsync<WorkflowInstanceDto>(pm, $"/api/v1/projects/{project.Id}/business-case/submit", null);
        Assert.Equal("InProgress", wf.State);
        Assert.Equal(new[] { "PMO_REVIEW", "CFO_BUDGET", "EXEC_APPROVE" }, wf.Steps.Select(s => s.Code)); // below the R50m Board band

        // The submitter never sees their own approval task.
        var pmInbox = await TetaApiFactory.GetAsync<List<InboxItemDto>>(pm, "/api/v1/inbox");
        Assert.DoesNotContain(pmInbox, i => i.InstanceId == wf.Id);

        var done = await _cast.ApproveAllAsync(wf.Id);
        Assert.Equal("Approved", done.State);

        var approved = await TetaApiFactory.GetAsync<ProjectDetailDto>(pm, $"/api/v1/projects/{project.Id}");
        Assert.Equal("Approved", approved.Status);
        Assert.Matches(@"^PRJ-\d{4}-\d{4}$", approved.ProjectNumber!);
        Assert.Equal(3_000_000m, approved.ApprovedBudget);
    }

    [Fact]
    public async Task Rejection_requires_a_reason_and_is_recorded()
    {
        var project = await _cast.NewProjectAsync("Rejected project");
        var pm = await _cast.PmAsync();
        await TetaApiFactory.PutAsync<BusinessCaseDto>(pm, $"/api/v1/projects/{project.Id}/business-case", new SaveBusinessCaseRequest(
            "P", "O", "Op", "S", "B", 1_000_000m, "R", "D", null, null));
        var wf = await TetaApiFactory.PostAsync<WorkflowInstanceDto>(pm, $"/api/v1/projects/{project.Id}/business-case/submit", null);

        var pmo = await _cast.PmoAsync();
        var inbox = await TetaApiFactory.GetAsync<List<InboxItemDto>>(pmo, "/api/v1/inbox");
        var task = inbox.Single(i => i.InstanceId == wf.Id);
        var noReason = await pmo.PostAsJsonAsync($"/api/v1/inbox/{task.TaskId}/decision", new { decision = TaskDecision.Rejected, comment = "" }, TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        await _cast.DecideAsync(Roles.HeadPmo, wf.Id, TaskDecision.Rejected, "Not aligned to the APP");
        var result = await TetaApiFactory.GetAsync<ProjectDetailDto>(pm, $"/api/v1/projects/{project.Id}");
        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.ProjectNumber);
    }

    [Fact]
    public async Task One_person_cannot_approve_two_steps_of_the_same_transaction()
    {
        var project = await _cast.NewProjectAsync("SoD project");
        var pm = await _cast.PmAsync();
        await TetaApiFactory.PutAsync<BusinessCaseDto>(pm, $"/api/v1/projects/{project.Id}/business-case", new SaveBusinessCaseRequest(
            "P", "O", "Op", "S", "B", 1_000_000m, "R", "D", null, null));
        var wf = await TetaApiFactory.PostAsync<WorkflowInstanceDto>(pm, $"/api/v1/projects/{project.Id}/business-case/submit", null);

        // A user holding both HeadPMO and CFO roles approves step 1, then tries step 2.
        _factory.CreateUser("plc.dualrole", Roles.HeadPmo, Roles.Cfo);
        var dual = await _factory.ClientForAsync("plc.dualrole");
        var first = (await TetaApiFactory.GetAsync<List<InboxItemDto>>(dual, "/api/v1/inbox")).Single(i => i.InstanceId == wf.Id);
        await TetaApiFactory.PostAsync<DecisionResult>(dual, $"/api/v1/inbox/{first.TaskId}/decision", new { decision = TaskDecision.Approved, comment = "ok" });

        var second = (await TetaApiFactory.GetAsync<List<InboxItemDto>>(dual, "/api/v1/inbox")).Single(i => i.InstanceId == wf.Id);
        var response = await dual.PostAsJsonAsync($"/api/v1/inbox/{second.TaskId}/decision", new { decision = TaskDecision.Approved, comment = "again" }, TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("BR-008", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Approval_above_delegated_authority_is_blocked()
    {
        // Remove the executive's unlimited ProjectApproval delegation, leaving R20m for CFO only.
        using (var db = _factory.CreateContext())
        {
            var exec = db.Delegations.Single(d => d.AuthorityType == AuthorityTypes.ProjectApproval && d.RoleCode == Roles.ExecutiveAuthority);
            exec.MaxAmount = 10_000_000m;
            db.SaveChanges();
        }
        try
        {
            var project = await _cast.NewProjectAsync("Big project");
            var pm = await _cast.PmAsync();
            await TetaApiFactory.PutAsync<BusinessCaseDto>(pm, $"/api/v1/projects/{project.Id}/business-case", new SaveBusinessCaseRequest(
                "P", "O", "Op", "S", "B", 15_000_000m, "R", "D", null, null));
            var wf = await TetaApiFactory.PostAsync<WorkflowInstanceDto>(pm, $"/api/v1/projects/{project.Id}/business-case/submit", null);
            await _cast.DecideAsync(Roles.HeadPmo, wf.Id);
            await _cast.DecideAsync(Roles.Cfo, wf.Id);
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _cast.DecideAsync(Roles.ExecutiveAuthority, wf.Id));
            Assert.Contains("BR-007", ex.Message);
        }
        finally
        {
            using var db = _factory.CreateContext();
            var exec = db.Delegations.Single(d => d.AuthorityType == AuthorityTypes.ProjectApproval && d.RoleCode == Roles.ExecutiveAuthority);
            exec.MaxAmount = 999_999_999_999m;
            db.SaveChanges();
        }
    }

    [Fact]
    public async Task Project_scoped_manager_cannot_see_other_projects()
    {
        var mine = await _cast.NewProjectAsync("Visible project");
        var other = await _cast.NewProjectAsync("Hidden project");

        _factory.CreateUser("plc.scopedpm", Teta.Ippcms.Domain.Security.ScopeType.Project, mine.Id, Roles.ProjectManager);
        var scoped = await _factory.ClientForAsync("plc.scopedpm");

        Assert.Equal(HttpStatusCode.OK, (await scoped.GetAsync($"/api/v1/projects/{mine.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scoped.GetAsync($"/api/v1/projects/{other.Id}")).StatusCode);
        var list = await TetaApiFactory.GetAsync<Platform.Core.PagedResult<ProjectListItemDto>>(scoped, "/api/v1/projects?pageSize=200");
        Assert.Contains(list.Items, p => p.Id == mine.Id);
        Assert.DoesNotContain(list.Items, p => p.Id == other.Id);
    }

    [Fact]
    public async Task Missing_permission_returns_403()
    {
        var auditor = await _factory.NewUserClientAsync("plc.auditor", Roles.InternalAudit);
        var response = await auditor.PostAsJsonAsync("/api/v1/portfolios", new SavePortfolioRequest("X", "X", null, null), TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
