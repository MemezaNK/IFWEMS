using Microsoft.AspNetCore.Mvc;
using Platform.Core;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.Monitoring;
using Teta.Ippcms.Domain.Security;
using MonitoringTemplateDto = Teta.Ippcms.Application.Monitoring.TemplateDto;
using SaveMonitoringTemplateRequest = Teta.Ippcms.Application.Monitoring.SaveTemplateRequest;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>M&amp;E plans, templates, visits, findings, corrective actions and beneficiaries (SRS §5.9 M&amp;E).</summary>
[Route("api/v1/me")]
public sealed class MonitoringController : TetaControllerBase
{
    private readonly IMonitoringService _me;

    public MonitoringController(IMonitoringService me) => _me = me;

    public sealed record RevealRequest(string Reason);
    public sealed record DuplicateDecisionRequest(bool IsDuplicate, string Note);

    [HttpGet("dashboard"), HasPermission(Permissions.MeRead)]
    public Task<MeDashboardDto> Dashboard([FromQuery] Guid? programmeId, [FromQuery] Guid? projectId, [FromQuery] Guid? providerId, CancellationToken ct) =>
        _me.DashboardAsync(programmeId, projectId, providerId, ct);

    [HttpGet("projects/{projectId:guid}/plan"), HasPermission(Permissions.MeRead)]
    public Task<MePlanDto?> Plan(Guid projectId, CancellationToken ct) => _me.GetPlanAsync(projectId, ct);

    [HttpPut("projects/{projectId:guid}/plan"), HasPermission(Permissions.MeManage)]
    public Task<MePlanDto> SavePlan(Guid projectId, SaveMePlanRequest request, CancellationToken ct) => _me.SavePlanAsync(projectId, request, ct);

    [HttpGet("templates"), HasPermission(Permissions.MeRead)]
    public Task<IReadOnlyList<MonitoringTemplateDto>> Templates([FromQuery] bool publishedOnly = false, CancellationToken ct = default) =>
        _me.ListTemplatesAsync(publishedOnly, ct);

    [HttpPost("templates"), HasPermission(Permissions.MeManage)]
    public Task<MonitoringTemplateDto> CreateTemplate(SaveMonitoringTemplateRequest request, CancellationToken ct) => _me.SaveTemplateAsync(null, request, ct);

    [HttpPut("templates/{id:guid}"), HasPermission(Permissions.MeManage)]
    public Task<MonitoringTemplateDto> UpdateTemplate(Guid id, SaveMonitoringTemplateRequest request, CancellationToken ct) => _me.SaveTemplateAsync(id, request, ct);

    [HttpPost("templates/{id:guid}/publish"), HasPermission(Permissions.MeManage)]
    public Task<MonitoringTemplateDto> PublishTemplate(Guid id, CancellationToken ct) => _me.PublishTemplateAsync(id, ct);

    [HttpGet("visits"), HasPermission(Permissions.MeRead)]
    public Task<IReadOnlyList<VisitDto>> Visits([FromQuery] Guid? projectId, [FromQuery] string? status, CancellationToken ct) => _me.ListVisitsAsync(projectId, status, ct);

    [HttpGet("visits/{id:guid}"), HasPermission(Permissions.MeRead)]
    public Task<VisitDto> Visit(Guid id, CancellationToken ct) => _me.GetVisitAsync(id, ct);

    [HttpPost("visits"), HasPermission(Permissions.MeManage)]
    public Task<VisitDto> Schedule(ScheduleVisitRequest request, CancellationToken ct) => _me.ScheduleVisitAsync(request, ct);

    [HttpPost("visits/{id:guid}/capture"), HasPermission(Permissions.MeManage)]
    public Task<VisitDto> Capture(Guid id, CaptureVisitRequest request, CancellationToken ct) => _me.CaptureVisitAsync(id, request, ct);

    [HttpGet("findings"), HasPermission(Permissions.MeRead)]
    public Task<IReadOnlyList<FindingDto>> Findings([FromQuery] Guid? projectId, [FromQuery] bool openOnly = false, CancellationToken ct = default) =>
        _me.ListFindingsAsync(projectId, openOnly, ct);

    [HttpPost("findings"), HasPermission(Permissions.MeManage)]
    public Task<FindingDto> AddFinding(SaveFindingRequest request, CancellationToken ct) => _me.SaveFindingAsync(null, request, ct);

    [HttpPut("findings/{id:guid}"), HasPermission(Permissions.MeManage)]
    public Task<FindingDto> UpdateFinding(Guid id, SaveFindingRequest request, CancellationToken ct) => _me.SaveFindingAsync(id, request, ct);

    /// <summary>Corrective actions are also assigned from audit/risk findings, so any authenticated owner can list their own.</summary>
    [HttpGet("actions")]
    public Task<IReadOnlyList<ActionDto>> Actions([FromQuery] string? parentType, [FromQuery] Guid? parentId, [FromQuery] bool openOnly = false,
        [FromQuery] bool mineOnly = false, CancellationToken ct = default) => _me.ListActionsAsync(parentType, parentId, openOnly, mineOnly, ct);

    [HttpPost("actions"), HasPermission(Permissions.MeManage, Permissions.AssuranceManage, Permissions.RiskManage)]
    public Task<ActionDto> AddAction(SaveActionRequest request, CancellationToken ct) => _me.SaveActionAsync(null, request, ct);

    [HttpPut("actions/{id:guid}"), HasPermission(Permissions.MeManage, Permissions.AssuranceManage, Permissions.RiskManage)]
    public Task<ActionDto> UpdateAction(Guid id, SaveActionRequest request, CancellationToken ct) => _me.SaveActionAsync(id, request, ct);

    [HttpPost("actions/{id:guid}/complete")]
    public Task<ActionDto> CompleteAction(Guid id, CompleteActionRequest request, CancellationToken ct) => _me.CompleteActionAsync(id, request, ct);

    [HttpGet("beneficiaries"), HasPermission(Permissions.BeneficiaryManage, Permissions.MeRead)]
    public Task<PagedResult<BeneficiaryDto>> Beneficiaries([FromQuery] Guid? projectId, [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        _me.ListBeneficiariesAsync(projectId, status, search, page, pageSize, ct);

    [HttpPost("beneficiaries"), HasPermission(Permissions.BeneficiaryManage)]
    public Task<BeneficiaryDto> Register(SaveBeneficiaryRequest request, CancellationToken ct) => _me.RegisterBeneficiaryAsync(request, ct);

    [HttpPost("beneficiaries/{id:guid}/status"), HasPermission(Permissions.BeneficiaryManage)]
    public Task<BeneficiaryDto> ChangeStatus(Guid id, BeneficiaryStatusRequest request, CancellationToken ct) => _me.ChangeBeneficiaryStatusAsync(id, request, ct);

    [HttpGet("beneficiaries/{id:guid}/history"), HasPermission(Permissions.BeneficiaryManage, Permissions.MeRead)]
    public Task<IReadOnlyList<BeneficiaryHistoryDto>> History(Guid id, CancellationToken ct) => _me.BeneficiaryHistoryAsync(id, ct);

    [HttpPost("beneficiaries/{id:guid}/reveal"), HasPermission(Permissions.BeneficiaryPii)]
    public async Task<object> Reveal(Guid id, RevealRequest request, CancellationToken ct) => new { identifier = await _me.RevealIdentifierAsync(id, request.Reason, ct) };

    [HttpPost("beneficiaries/{id:guid}/duplicate-decision"), HasPermission(Permissions.BeneficiaryManage)]
    public Task<BeneficiaryDto> ResolveDuplicate(Guid id, DuplicateDecisionRequest request, CancellationToken ct) =>
        _me.ResolveDuplicateAsync(id, request.IsDuplicate, request.Note, ct);
}
