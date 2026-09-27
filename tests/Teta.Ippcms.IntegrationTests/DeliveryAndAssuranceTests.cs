using Teta.Ippcms.Application.Assurance;
using Teta.Ippcms.Application.Documents;
using Teta.Ippcms.Application.Execution;
using Teta.Ippcms.Application.Monitoring;
using Teta.Ippcms.Application.Strategy;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Execution;
using Teta.Ippcms.Domain.Monitoring;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.IntegrationTests;

/// <summary>UAT: schedule/WBS, change control, health, M&amp;E with protected beneficiary data, risk rating and APP results with evidence.</summary>
public sealed class DeliveryAndAssuranceTests : IClassFixture<ScenarioFixture>
{
    private readonly TetaApiFactory _factory;
    private readonly Cast _cast;

    public DeliveryAndAssuranceTests(ScenarioFixture fixture)
    {
        _factory = fixture.Factory;
        _cast = fixture.Cast;
    }

    private static Task<T> Post<T>(HttpClient c, string url, object? body) => TetaApiFactory.PostAsync<T>(c, url, body);
    private static Task<T> Get<T>(HttpClient c, string url) => TetaApiFactory.GetAsync<T>(c, url);
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Schedule_dependencies_baseline_change_request_and_health()
    {
        var project = await _cast.ApprovedProjectAsync("Schedule project", 1_000_000m);
        var pm = await _cast.PmAsync();
        var ws = await Post<WbsDto>(pm, $"/api/v1/projects/{project.Id}/wbs", new SaveWbsRequest(null, WbsType.Workstream, "1", "Delivery", null, "PM",
            Today, Today.AddMonths(6), 1, false, null, false, 1));
        var a = await Post<WbsDto>(pm, $"/api/v1/projects/{project.Id}/wbs", new SaveWbsRequest(ws.Id, WbsType.Activity, "1.1", "Recruit learners", null, "PM",
            Today, Today.AddMonths(1), 1, false, null, false, 1));
        var m = await Post<WbsDto>(pm, $"/api/v1/projects/{project.Id}/wbs", new SaveWbsRequest(ws.Id, WbsType.Milestone, "1.2", "Learners enrolled", null, "PM",
            Today.AddMonths(1), Today.AddMonths(1), 1, true, "Enrolment register", false, 2));
        await Post<DependencyDto>(pm, $"/api/v1/projects/{project.Id}/dependencies", new SaveDependencyRequest(a.Id, m.Id, DependencyType.FS, 0));
        var cycle = await pm.PostAsJsonAsync($"/api/v1/projects/{project.Id}/dependencies", new SaveDependencyRequest(m.Id, a.Id, DependencyType.FS, 0), TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cycle.StatusCode); // circular dependency rejected

        var progressed = await Post<WbsDto>(pm, $"/api/v1/projects/{project.Id}/wbs/{a.Id}/progress", new ProgressRequest(50, null, Today, null, "Half recruited"));
        Assert.Equal(50m, progressed.PercentComplete);
        var gantt = await Get<GanttDto>(pm, $"/api/v1/projects/{project.Id}/gantt");
        Assert.NotNull(gantt);

        var pmo = await _cast.PmoAsync();
        await Post<BaselineDto>(pmo, $"/api/v1/projects/{project.Id}/baseline", new { reason = "Initial baseline" });

        var change = await Post<ChangeRequestDto>(pm, "/api/v1/change-requests", new SaveChangeRequest(project.Id, null, ChangeType.Schedule, "Extend enrolment",
            "Recruitment slower than planned", "Provider capacity", 0m, 30, Today.AddMonths(13), null, null, null));
        var impact = await Get<ImpactAssessmentDto>(pm, $"/api/v1/change-requests/{change.Id}/impact");
        Assert.NotNull(impact);
        var cwf = await Post<WorkflowInstanceDto>(pm, $"/api/v1/change-requests/{change.Id}/submit", null);
        Assert.Equal("Approved", (await _cast.ApproveAllAsync(cwf.Id)).State);
        var changes = await Get<List<ChangeRequestDto>>(pm, $"/api/v1/change-requests?projectId={project.Id}");
        Assert.Equal("Approved", changes.Single().Status);

        await Post<IssueDto>(pm, "/api/v1/issues", new SaveIssueRequest(project.Id, "Provider delay", "Late start", Severity.Critical, null, "PM", "Escalate",
            Today.AddDays(-3), null, null));
        var health = await Post<HealthDto>(pm, $"/api/v1/projects/{project.Id}/health", null);
        Assert.False(string.IsNullOrWhiteSpace(health.Explanation));
        var status = await Get<StatusReportDto>(pm, $"/api/v1/projects/{project.Id}/status-report");
        Assert.NotNull(status);
    }

    [Fact]
    public async Task Monitoring_visit_finding_action_and_protected_beneficiaries()
    {
        var project = await _cast.ApprovedProjectAsync("M&E project", 800_000m);
        var me = await _cast.AsAsync(Roles.MeOfficer);
        await TetaApiFactory.PutAsync<MePlanDto>(me, $"/api/v1/me/projects/{project.Id}/plan",
            new SaveMePlanRequest("Quarterly site monitoring", "Quarterly", "Site visits", null, null, "M&E officer", Today.AddDays(7)));
        var visit = await Post<VisitDto>(me, "/api/v1/me/visits", new ScheduleVisitRequest(project.Id, null, VisitType.Site, Today, "M&E team", "Durban", null));
        var captured = await Post<VisitDto>(me, $"/api/v1/me/visits/{visit.Id}/capture", new CaptureVisitRequest(Today, "M&E team", "Durban", -29.85m, 31.02m,
            new Dictionary<string, string?>(), "Attendance below target", "Corrective action required", true));
        Assert.Equal("Completed", captured.Status);
        var finding = await Post<FindingDto>(me, "/api/v1/me/findings", new SaveFindingRequest(project.Id, visit.Id, "Attendance registers incomplete", Severity.High, "Poor controls"));

        var pm = await _cast.PmAsync();
        var action = await Post<ActionDto>(me, "/api/v1/me/actions", new SaveActionRequest(ParentTypes.Finding, finding.Id, "Reconcile registers",
            _cast.UserIds[Roles.ProjectManager], "PM", Today.AddDays(14)));
        var mine = await Get<List<ActionDto>>(pm, "/api/v1/me/actions?mineOnly=true&openOnly=true");
        Assert.Contains(mine, x => x.Id == action.Id);
        var noEvidence = await pm.PostAsJsonAsync($"/api/v1/me/actions/{action.Id}/complete", new CompleteActionRequest("Done"), TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noEvidence.StatusCode); // closure needs evidence
        await TetaApiFactory.UploadEvidenceAsync(pm, ParentTypes.CorrectiveAction, action.Id, "Completion report");
        var done = await Post<ActionDto>(pm, $"/api/v1/me/actions/{action.Id}/complete", new CompleteActionRequest("Registers reconciled and filed"));
        Assert.Equal("Completed", done.Status);

        // Beneficiary: ID encrypted, masked, Luhn-validated, de-duplicated; reveal needs PII permission and is audited.
        var badId = await me.PostAsJsonAsync("/api/v1/me/beneficiaries", new SaveBeneficiaryRequest(project.Id, "8001015009088", "SAID", "Thabo", "Mokoena",
            "M", 1980, "KwaZulu-Natal", "eThekwini", "Learnership", null, "Discretionary grant", true), TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, badId.StatusCode);
        var ben = await Post<BeneficiaryDto>(me, "/api/v1/me/beneficiaries", new SaveBeneficiaryRequest(project.Id, "8001015009087", "SAID", "Thabo", "Mokoena",
            "M", 1980, "KwaZulu-Natal", "eThekwini", "Learnership", null, "Discretionary grant", true));
        Assert.DoesNotContain("8001015009087", ben.IdentifierMasked);
        var sameProject = await me.PostAsJsonAsync("/api/v1/me/beneficiaries", new SaveBeneficiaryRequest(project.Id, "8001015009087", "SAID", "Thabo",
            "Mokoena", "M", 1980, "KwaZulu-Natal", "eThekwini", "Learnership", null, "Discretionary grant", true), TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, sameProject.StatusCode); // same person, project and intervention
        var otherProject = await _cast.ApprovedProjectAsync("Second M&E project", 500_000m);
        var again = await Post<BeneficiaryDto>(me, "/api/v1/me/beneficiaries", new SaveBeneficiaryRequest(otherProject.Id, "8001015009087", "SAID", "Thabo",
            "Mokoena", "M", 1980, "KwaZulu-Natal", "eThekwini", "Learnership", null, "Discretionary grant", true));
        Assert.True(again.PotentialDuplicate); // flagged for review across projects (FR-ME-008)

        using (var db = _factory.CreateContext())
        {
            var stored = db.Beneficiaries.Single(b => b.Id == ben.Id);
            Assert.StartsWith("v1:", stored.IdentifierEncrypted);
            Assert.DoesNotContain("8001015009087", stored.IdentifierEncrypted);
        }
        var noPii = await pm.PostAsJsonAsync($"/api/v1/me/beneficiaries/{ben.Id}/reveal", new { reason = "check" }, TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.Forbidden, noPii.StatusCode);
        var revealed = await me.PostAsJsonAsync($"/api/v1/me/beneficiaries/{ben.Id}/reveal", new { reason = "Verification of stipend payment" }, TetaApiFactory.Json);
        Assert.Contains("8001015009087", await revealed.Content.ReadAsStringAsync());
        using (var db = _factory.CreateContext())
        {
            Assert.Contains(db.AuditLogs.ToList(), l => l.EntityId == ben.Id.ToString() && l.Action.Contains("Reveal"));
        }

        var dashboard = await Get<MeDashboardDto>(me, "/api/v1/me/dashboard");
        Assert.NotNull(dashboard);
    }

    [Fact]
    public async Task Risk_rating_heat_map_and_critical_escalation()
    {
        var project = await _cast.ApprovedProjectAsync("Risk project", 600_000m);
        var risk = await _cast.AsAsync(Roles.RiskCompliance);
        var r = await Post<RiskDto>(risk, "/api/v1/assurance/risks", new SaveRiskRequest(ParentTypes.Project, project.Id, "Provider failure", "Weak provider",
            "Provider cannot deliver", "Learners not trained", "Delivery", 5, 5, 4, 5, null, "Risk owner", Today.AddDays(30), null));
        Assert.Equal(25, r.InherentScore);
        Assert.Equal("Critical", r.InherentRating);
        Assert.Equal("Critical", r.ResidualRating);
        var treatment = await Post<TreatmentDto>(risk, $"/api/v1/assurance/risks/{r.Id}/treatments",
            new SaveTreatmentRequest("Appoint backup provider", null, "PM", Today.AddDays(-1), null));
        Assert.True(treatment.IsOverdue);
        var heat = await Get<HeatMapDto>(risk, $"/api/v1/assurance/heat-map?projectId={project.Id}");
        Assert.Equal(1, heat.Cells.Single(c => c.Likelihood == 4 && c.Impact == 5).Count);
        var dashboard = await Get<AssuranceDashboardDto>(risk, "/api/v1/assurance/dashboard");
        Assert.True(dashboard.CriticalRisks >= 1);
        using var db = _factory.CreateContext();
        Assert.Contains(db.EscalationEvents.ToList(), e => e.EntityId == r.Id);
    }

    [Fact]
    public async Task App_results_require_verified_evidence_and_segregated_verification()
    {
        var project = await _cast.ApprovedProjectAsync("APP project", 700_000m);
        var indicatorId = await _cast.IndicatorAsync();
        var pm = await _cast.PmAsync();
        var result = await Post<ResultDto>(pm, "/api/v1/strategy/results", new CaptureResultRequest(indicatorId, project.Id, "2026/27", 1, 40, "40 learners enrolled"));
        await Post<ResultDto>(pm, $"/api/v1/strategy/results/{result.Id}/submit", null);

        var strategy = await _cast.AsAsync(Roles.StrategyOfficer);
        var noEvidence = await strategy.PostAsJsonAsync($"/api/v1/strategy/results/{result.Id}/verify", new { approve = true }, TetaApiFactory.Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noEvidence.StatusCode); // BR-009

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent("%PDF agreements"u8.ToArray()), "File", "agreements.pdf" },
            { new StringContent(ParentTypes.PerformanceResult), "ParentType" },
            { new StringContent(result.Id.ToString()), "ParentId" },
            { new StringContent("Learner agreement"), "DocumentType" },
            { new StringContent("Learner agreement"), "EvidenceType" },
            { new StringContent("Internal"), "Classification" }
        };
        var evidence = await TetaApiFactory.ReadAsync<EvidenceDto>(await pm.PostAsync("/api/v1/documents/evidence", form));

        // The uploader cannot verify their own evidence (SoD); the strategy officer can.
        _factory.CreateUser("app.pmverifier", Roles.ProjectManager, Roles.StrategyOfficer);
        var me = await _cast.AsAsync(Roles.MeOfficer);
        await Post<EvidenceDto>(strategy, $"/api/v1/documents/evidence/{evidence.Id}/verify", new VerifyEvidenceRequest(true, "Agreements checked"));
        var verified = await Post<ResultDto>(strategy, $"/api/v1/strategy/results/{result.Id}/verify", new { approve = true, comment = "Verified" });
        Assert.Equal("Verified", verified.Status);

        var performance = await Get<List<IndicatorPerformanceDto>>(strategy, "/api/v1/strategy/performance?financialYear=2026%2F27");
        var row = performance.Single(p => p.IndicatorId == indicatorId);
        Assert.Equal(40m, row.VerifiedActual);
        Assert.Contains(row.ContributingProjects, c => c.ProjectId == project.Id);
        _ = me;
    }
}
