using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Security.Mfa;
using Teta.Ippcms.Application.Admin;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Home;
using Teta.Ippcms.Application.Jobs;
using Teta.Ippcms.Application.Reporting;
using Teta.Ippcms.Application.Search;
using Teta.Ippcms.Application.Security;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.IntegrationTests;

/// <summary>UAT: authentication/MFA/session security, dual control, configuration audit, imports, search, reporting catalogue and background jobs.</summary>
public sealed class PlatformAndReportingTests : IClassFixture<ScenarioFixture>
{
    private readonly TetaApiFactory _factory;
    private readonly Cast _cast;

    public PlatformAndReportingTests(ScenarioFixture fixture)
    {
        _factory = fixture.Factory;
        _cast = fixture.Cast;
    }

    private static Task<T> Post<T>(HttpClient c, string url, object? body) => TetaApiFactory.PostAsync<T>(c, url, body);
    private static Task<T> Get<T>(HttpClient c, string url) => TetaApiFactory.GetAsync<T>(c, url);

    // ---------------- Security ----------------

    [Fact]
    public async Task Privileged_role_must_enrol_and_then_use_mfa()
    {
        _factory.CreateUser("sec.cfo", Roles.Cfo);
        var client = _factory.CreateClient();
        var first = await Post<LoginResult>(client, "/api/v1/auth/login", new LoginRequest("sec.cfo", TetaApiFactory.Password));
        Assert.True(first.MfaEnrolmentRequired);
        Assert.Null(first.AccessToken);
        var enrol = await Post<MfaEnrolmentDto>(client, "/api/v1/auth/mfa/enrol", new { challengeToken = first.ChallengeToken });
        var totp = new TotpService();
        var wrong = await Post<LoginResult>(client, "/api/v1/auth/mfa/enrol/confirm", new MfaConfirmRequest(enrol.ChallengeToken, "000000"));
        Assert.False(wrong.Succeeded);
        var ok = await Post<LoginResult>(client, "/api/v1/auth/mfa/enrol/confirm", new MfaConfirmRequest(enrol.ChallengeToken, totp.ComputeCode(enrol.Secret, DateTime.UtcNow)));
        Assert.True(ok.Succeeded);

        var second = await Post<LoginResult>(client, "/api/v1/auth/login", new LoginRequest("sec.cfo", TetaApiFactory.Password));
        Assert.True(second.MfaRequired);
        var verified = await Post<LoginResult>(client, "/api/v1/auth/mfa/verify", new MfaVerifyRequest(second.ChallengeToken!, totp.ComputeCode(enrol.Secret, DateTime.UtcNow)));
        Assert.True(verified.Succeeded);
        Assert.NotNull(verified.AccessToken);
    }

    [Fact]
    public async Task Repeated_failed_logins_lock_the_account()
    {
        _factory.CreateUser("sec.lockme", Roles.ProjectManager);
        var client = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
            Assert.False((await Post<LoginResult>(client, "/api/v1/auth/login", new LoginRequest("sec.lockme", "wrong-password"))).Succeeded);
        var locked = await Post<LoginResult>(client, "/api/v1/auth/login", new LoginRequest("sec.lockme", TetaApiFactory.Password));
        Assert.False(locked.Succeeded);
        Assert.Contains("locked", locked.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Users_without_a_teta_role_are_refused()
    {
        _factory.CreateUser("sec.ifwemsonly");
        var result = await Post<LoginResult>(_factory.CreateClient(), "/api/v1/auth/login", new LoginRequest("sec.ifwemsonly", TetaApiFactory.Password));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Logout_revokes_the_server_side_session()
    {
        var client = await _factory.NewUserClientAsync("sec.logout", Roles.ProjectManager);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Role_assignment_needs_a_second_security_administrator()
    {
        var sec1 = await _cast.AsAsync(Roles.SecurityAdministrator);
        _factory.CreateUser("sec.secadmin2", Roles.SecurityAdministrator);
        var sec2 = await _factory.ClientForAsync("sec.secadmin2");
        var target = _factory.CreateUser("sec.newpm");

        var request = await Post<AssignmentDto>(sec1, "/api/v1/security/assignments", new RequestAssignmentRequest(target, Roles.ProjectManager, ScopeType.Global, null,
            DateOnly.FromDateTime(DateTime.UtcNow), null, "New project manager"));
        Assert.Equal("PendingApproval", request.Status);
        var self = await sec1.PostAsJsonAsync($"/api/v1/security/assignments/{request.Id}/decision", new DecideAssignmentRequest(true, null), TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, self.StatusCode);
        var approved = await Post<AssignmentDto>(sec2, $"/api/v1/security/assignments/{request.Id}/decision", new DecideAssignmentRequest(true, "Verified with HR"));
        Assert.Equal("Active", approved.Status);
        var pm = await _factory.ClientForAsync("sec.newpm");
        Assert.Equal(HttpStatusCode.OK, (await pm.GetAsync("/api/v1/home")).StatusCode);

        // System administrators may never receive business approval permissions (SoD).
        var escalate = await sec2.PutAsJsonAsync($"/api/v1/security/roles/{Roles.SystemAdministrator}/permissions",
            new SaveRolePermissionsRequest(new[] { Permissions.AdminConfig, Permissions.ProjectApprove }), TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, escalate.StatusCode);
    }

    [Fact]
    public async Task Responses_carry_security_headers_and_correlation_id()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live");
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    // ---------------- Administration ----------------

    [Fact]
    public async Task Workflow_definition_changes_are_versioned_and_dual_controlled()
    {
        var sysadmin = await _cast.AsAsync(Roles.SystemAdministrator);
        var defs = await Get<List<WorkflowDefinitionDto>>(sysadmin, "/api/v1/admin/workflows");
        var closure = defs.Single(d => d.Code == "PROJECT_CLOSURE" && d.Status == "Approved");
        var draft = await Post<WorkflowDefinitionDto>(sysadmin, "/api/v1/admin/workflows", new SaveWorkflowDefinitionRequest(closure.Code, closure.Name, closure.EntityType,
            "Adds CFO sign-off", closure.Steps.Select(s => new StepInput(s.StepOrder, s.Code, s.Name, s.RequiredRole, s.AuthorityType, s.MinimumValue, s.MaximumValue,
                s.SlaHours, s.EscalationRole)).Append(new StepInput(30, "CFO_SIGNOFF", "CFO sign-off", Roles.Cfo, null, null, null, 72, null)).ToList()));
        Assert.Equal(2, draft.DefinitionVersion);
        await Post<WorkflowDefinitionDto>(sysadmin, $"/api/v1/admin/workflows/{draft.Id}/submit", null);

        var cfo = await _cast.CfoAsync(); // holds admin.config.approve
        var activated = await Post<WorkflowDefinitionDto>(cfo, $"/api/v1/admin/workflows/{draft.Id}/activate", new { approve = true, comment = "Approved" });
        Assert.Equal("Approved", activated.Status);
        var after = await Get<List<WorkflowDefinitionDto>>(sysadmin, "/api/v1/admin/workflows");
        Assert.Equal("Retired", after.Single(d => d.Id == closure.Id).Status);
    }

    [Fact]
    public async Task Setting_changes_keep_old_and_new_values_in_the_audit_trail()
    {
        var sysadmin = await _cast.AsAsync(Roles.SystemAdministrator);
        await TetaApiFactory.PutAsync<SettingDto>(sysadmin, $"/api/v1/admin/settings/{SettingKeys.ExceptionProcurementAgeingDays}", new SaveSettingRequest("60"));
        using var db = _factory.CreateContext();
        var entries = db.AuditLogs.Where(a => a.EntityType == "SystemSetting").ToList();
        Assert.Contains(entries, a => a.NewValues != null && a.NewValues.Contains("60"));
    }

    [Fact]
    public async Task Bulk_import_validates_and_reports_errors()
    {
        var sysadmin = await _cast.AsAsync(Roles.SystemAdministrator);
        const string csv = "Category,Code,Name,SortOrder\nCostCategory,CATERING,Catering,10\nCostCategory,,Missing code,x\n";
        var result = await PostCsv(sysadmin, "/api/v1/admin/imports/ReferenceData?validateOnly=false", csv);
        Assert.Equal("Rejected", result.Status); // all-or-nothing
        Assert.Contains(result.Errors, e => e.Row == 3 && e.Field == "Code");
        Assert.Contains(result.Errors, e => e.Row == 3 && e.Field == "SortOrder");
        using (var db = _factory.CreateContext()) Assert.False(db.ReferenceData.Any(r => r.Code == "CATERING"));

        var good = await PostCsv(sysadmin, "/api/v1/admin/imports/ReferenceData?validateOnly=false", "Category,Code,Name\nCostCategory,CATERING,Catering\n");
        Assert.Equal("Completed", good.Status);
        using (var db = _factory.CreateContext()) Assert.True(db.ReferenceData.Any(r => r.Code == "CATERING"));
    }

    private static async Task<ImportResultDto> PostCsv(HttpClient client, string url, string csv)
    {
        using var form = new MultipartFormDataContent { { new StringContent(csv), "file", "import.csv" } };
        return await TetaApiFactory.ReadAsync<ImportResultDto>(await client.PostAsync(url, form));
    }

    [Fact]
    public async Task Global_search_respects_row_level_scope()
    {
        var visible = await _cast.NewProjectAsync("Searchable Welding Project");
        var hidden = await _cast.NewProjectAsync("Searchable Welding Secret");
        _factory.CreateUser("sec.searcher", ScopeType.Project, visible.Id, Roles.ProjectManager);
        var scoped = await _factory.ClientForAsync("sec.searcher");
        var result = await Get<SearchResponse>(scoped, "/api/v1/search?q=Searchable%20Welding");
        Assert.Contains(result.Results, r => r.Id == visible.Id);
        Assert.DoesNotContain(result.Results, r => r.Id == hidden.Id);
    }

    // ---------------- Reporting ----------------

    [Fact]
    public async Task Every_catalogue_report_runs_and_exports()
    {
        await _cast.ApprovedProjectAsync("Reporting project", 1_200_000m);
        var exec = await _cast.ExecAsync();
        var catalogue = await Get<List<ReportDefinition>>(exec, "/api/v1/reports/catalogue");
        Assert.Equal(15, catalogue.Count);
        foreach (var report in catalogue)
        {
            var table = await Get<ReportTable>(exec, $"/api/v1/reports/run/{report.Code}?financialYear=2026%2F27");
            Assert.Equal(report.Code, table.Code);
            Assert.NotEmpty(table.Columns);
        }
        foreach (var format in new[] { "csv", "xlsx", "pdf" })
        {
            var file = await exec.GetAsync($"/api/v1/reports/run/RPT-002/export?format={format}");
            Assert.Equal(HttpStatusCode.OK, file.StatusCode);
            Assert.True((await file.Content.ReadAsByteArrayAsync()).Length > 50);
        }
        var badFormat = await exec.GetAsync("/api/v1/reports/run/RPT-002/export?format=doc");
        Assert.Equal(HttpStatusCode.BadRequest, badFormat.StatusCode);

        var dashboard = await Get<ExecutiveDashboardDto>(exec, "/api/v1/reports/executive-dashboard?financialYear=2026%2F27");
        Assert.Contains(dashboard.Kpis, k => k.Code == "ACTIVE_PROJECTS");
        Assert.All(dashboard.Kpis, k => Assert.StartsWith("/", k.DrillLink));
        Assert.NotNull(await Get<List<ExceptionItemDto>>(exec, "/api/v1/reports/exceptions"));
        var analytics = await Get<ReportTable>(exec, "/api/v1/reports/analytics");
        Assert.Contains(analytics.Rows, r => Equals(r[1]?.ToString(), "Reporting project"));
        var geo = await Get<GeoViewDto>(exec, "/api/v1/reports/geographic?level=Province");
        Assert.Contains(geo.Areas, a => a.Province == "Gauteng");
    }

    [Fact]
    public async Task Row_level_security_applies_to_reports()
    {
        var mine = await _cast.NewProjectAsync("Scoped report project");
        await _cast.NewProjectAsync("Other report project");
        _factory.CreateUser("rpt.scoped", ScopeType.Project, mine.Id, Roles.ProjectManager);
        var scoped = await _factory.ClientForAsync("rpt.scoped");
        var table = await Get<ReportTable>(scoped, "/api/v1/reports/run/RPT-002");
        Assert.Single(table.Rows);
        Assert.Equal("Scoped report project", table.Rows[0][1]?.ToString());
        var forbidden = await scoped.GetAsync("/api/v1/reports/run/RPT-003"); // procurement.read only
        Assert.Equal(HttpStatusCode.OK, forbidden.StatusCode);
        var auditOnly = await scoped.GetAsync("/api/v1/reports/run/RPT-009"); // supplier.read
        Assert.Equal(HttpStatusCode.OK, auditOnly.StatusCode);
    }

    [Fact]
    public async Task Board_pack_is_versioned_and_approved_by_someone_else()
    {
        var exec = await _cast.ExecAsync();
        var pack1 = await Post<BoardPackDto>(exec, "/api/v1/reports/board-packs", new GenerateBoardPackRequest("2026/27 Q2", "Q2 Board pack", "2026/27"));
        var pack2 = await Post<BoardPackDto>(exec, "/api/v1/reports/board-packs", new GenerateBoardPackRequest("2026/27 Q2", "Q2 Board pack (revised)", "2026/27"));
        Assert.Equal(1, pack1.PackVersion);
        Assert.Equal(2, pack2.PackVersion);
        var self = await exec.PostAsync($"/api/v1/reports/board-packs/{pack2.Id}/approve", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, self.StatusCode);
        var board = await _cast.AsAsync(Roles.Board);
        var approved = await Post<BoardPackDto>(board, $"/api/v1/reports/board-packs/{pack2.Id}/approve", null);
        Assert.Equal("Approved", approved.Status);
        var detail = await Get<BoardPackDetailDto>(board, $"/api/v1/reports/board-packs/{pack1.Id}");
        Assert.Equal(JsonValueKind.Object, detail.Dataset.ValueKind);
    }

    [Fact]
    public async Task Scheduled_report_runs_as_owner_and_queues_email_with_attachment()
    {
        var pmo = await _cast.PmoAsync();
        var schedule = await Post<ReportScheduleDto>(pmo, "/api/v1/reports/schedules",
            new SaveScheduleRequest("RPT-002", "xlsx", "Weekly", "pmo@test.local", true, null, null, null));
        Assert.NotNull(schedule.NextRunAtUtc);
        using (var db = _factory.CreateContext())
        {
            var s = db.ReportSchedules.Single(x => x.Id == schedule.Id);
            s.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-1);
            db.SaveChanges();
        }
        using (var scope = _factory.Services.CreateScope())
        {
            var sent = await scope.ServiceProvider.GetRequiredService<IReportingService>().RunDueSchedulesAsync(CancellationToken.None);
            Assert.True(sent >= 1);
        }
        using (var db = _factory.CreateContext())
        {
            var outbox = db.OutboxMessages.ToList().Last(m => m.PayloadJson.Contains("RPT-002"));
            var payload = JsonSerializer.Deserialize<EmailOutboxPayload>(outbox.PayloadJson)!;
            Assert.EndsWith(".xlsx", payload.AttachmentName);
            Assert.Contains("pmo@test.local", payload.To);
        }
        // Disable it again (FR-REP-006 acceptance: schedule can be disabled/changed).
        var disabled = await TetaApiFactory.PutAsync<ReportScheduleDto>(pmo, $"/api/v1/reports/schedules/{schedule.Id}",
            new SaveScheduleRequest("RPT-002", "pdf", "Monthly", "pmo@test.local", false, null, null, null));
        Assert.False(disabled.IsEnabled);
        Assert.Null(disabled.NextRunAtUtc);
    }

    [Fact]
    public async Task Data_quality_scan_raises_owned_issues_that_can_be_resolved()
    {
        var project = await _cast.ApprovedProjectAsync("DQ project", 900_000m); // approved, no budget lines -> DQ-PRJ-003
        var sysadmin = await _cast.AsAsync(Roles.SystemAdministrator);
        await Post<JsonElement>(sysadmin, "/api/v1/reports/data-quality/scan", null);
        var dq = await Get<DataQualityDashboardDto>(sysadmin, "/api/v1/reports/data-quality?status=Open");
        var issue = dq.Issues.First(i => i.RuleCode == "DQ-PRJ-003" && i.EntityId == project.Id);
        var resolved = await Post<DataQualityIssueDto>(sysadmin, $"/api/v1/reports/data-quality/{issue.Id}/resolve",
            new ResolveIssueRequest(Teta.Ippcms.Domain.Reporting.DataQualityStatus.Closed, "Budget captured offline; accepted"));
        Assert.Equal("Closed", resolved.Status);
        // A closed issue is not re-raised by the next scan.
        await Post<JsonElement>(sysadmin, "/api/v1/reports/data-quality/scan", null);
        var again = await Get<DataQualityDashboardDto>(sysadmin, "/api/v1/reports/data-quality?status=Open");
        Assert.DoesNotContain(again.Issues, i => i.RuleCode == "DQ-PRJ-003" && i.EntityId == project.Id);
    }

    // ---------------- Home & jobs ----------------

    [Fact]
    public async Task Home_page_shows_approvals_for_approvers()
    {
        var project = await _cast.NewProjectAsync("Home page project");
        var pm = await _cast.PmAsync();
        await TetaApiFactory.PutAsync<Teta.Ippcms.Application.Projects.BusinessCaseDto>(pm, $"/api/v1/projects/{project.Id}/business-case",
            new Teta.Ippcms.Application.Projects.SaveBusinessCaseRequest("P", "O", "Op", "S", "B", 100_000m, "R", "D", null, null));
        await Post<Teta.Ippcms.Application.Workflow.WorkflowInstanceDto>(pm, $"/api/v1/projects/{project.Id}/business-case/submit", null);
        var home = await Get<HomeDto>(await _cast.PmoAsync(), "/api/v1/home");
        Assert.Contains(home.Approvals, a => a.EntityReference == project.Reference);
        Assert.Contains(home.Kpis, k => k.Label == "Approvals waiting");
        Assert.True(home.UnreadAlerts >= 1); // task-assignment notification
    }

    [Fact]
    public async Task Background_jobs_escalate_overdue_items_and_alert_on_expiring_contracts()
    {
        var project = await _cast.ApprovedProjectAsync("Jobs project", 400_000m);
        var pm = await _cast.PmAsync();
        await Post<Teta.Ippcms.Application.Execution.IssueDto>(pm, "/api/v1/issues", new Teta.Ippcms.Application.Execution.SaveIssueRequest(project.Id,
            "Overdue issue", "Needs escalation", Teta.Ippcms.Domain.Common.Severity.High, _cast.UserIds[Roles.ProjectManager], "PM", null,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10), null, null));
        using (var db = _factory.CreateContext())
        {
            var supplier = new Teta.Ippcms.Domain.Suppliers.Supplier { SupplierNumber = "SUP-JOB-1", LegalName = "Jobs Supplier", NormalizedName = "JOBS SUPPLIER" };
            db.Suppliers.Add(supplier);
            db.Contracts.Add(new Contract
            {
                ContractNumber = "CNT-JOB-0001", Title = "Expiring contract", ProjectId = project.Id, SupplierId = supplier.Id, Source = ContractSource.NonBid,
                OriginalValue = 100_000m, StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-11),
                OriginalEndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20), CurrentEndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20),
                Status = ContractStatus.Active, ContractManagerUserId = _cast.UserIds[Roles.ProjectManager]
            });
            db.SaveChanges();
        }
        using var scope = _factory.Services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IScheduledJobs>();
        Assert.True(await jobs.EscalateOverdueItemsAsync(CancellationToken.None) >= 1);
        Assert.Equal(0, await jobs.EscalateOverdueItemsAsync(CancellationToken.None)); // not re-escalated within 7 days
        Assert.True(await jobs.ContractExpiryAlertsAsync(CancellationToken.None) >= 1);
        Assert.Equal(0, await jobs.ContractExpiryAlertsAsync(CancellationToken.None)); // idempotent within the lead-time window
        await jobs.DispatchOutboxAsync(CancellationToken.None);
        using var db2 = _factory.CreateContext();
        Assert.Contains(db2.EscalationEvents.ToList(), e => e.EntityReference != null && e.Reason.Contains("Issue"));
        Assert.All(db2.OutboxMessages.ToList(), m => Assert.NotNull(m.ProcessedAtUtc)); // SMTP disabled in tests -> marked processed
    }
}
