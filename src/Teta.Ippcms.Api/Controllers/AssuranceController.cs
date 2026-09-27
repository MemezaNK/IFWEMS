using Microsoft.AspNetCore.Mvc;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.Assurance;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Risk registers, controls, treatments, heat maps, compliance, audit findings and combined assurance (SRS §5.9).</summary>
[Route("api/v1/assurance")]
[HasPermission(Permissions.RiskRead, Permissions.RiskManage)]
public sealed class AssuranceController : TetaControllerBase
{
    private readonly IRiskService _risk;

    public AssuranceController(IRiskService risk) => _risk = risk;

    [HttpGet("dashboard")]
    public Task<AssuranceDashboardDto> Dashboard([FromQuery] Guid? projectId, CancellationToken ct) => _risk.DashboardAsync(projectId, ct);

    [HttpGet("rating-bands")]
    public Task<IReadOnlyList<BandDto>> Bands(CancellationToken ct) => _risk.GetBandsAsync(ct);

    [HttpPut("rating-bands"), HasPermission(Permissions.AssuranceManage)]
    public Task<IReadOnlyList<BandDto>> SaveBands(SaveBandsRequest request, CancellationToken ct) => _risk.SaveBandsAsync(request, ct);

    [HttpGet("risks")]
    public Task<IReadOnlyList<RiskDto>> Risks([FromQuery] Guid? projectId, [FromQuery] string? parentType, [FromQuery] Guid? parentId, [FromQuery] string? rating,
        [FromQuery] bool openOnly = true, CancellationToken ct = default) => _risk.ListRisksAsync(projectId, parentType, parentId, rating, openOnly, ct);

    [HttpGet("risks/{id:guid}")]
    public Task<RiskDto> Risk(Guid id, CancellationToken ct) => _risk.GetRiskAsync(id, ct);

    [HttpPost("risks"), HasPermission(Permissions.RiskManage)]
    public Task<RiskDto> AddRisk(SaveRiskRequest request, CancellationToken ct) => _risk.SaveRiskAsync(null, request, ct);

    [HttpPut("risks/{id:guid}"), HasPermission(Permissions.RiskManage)]
    public Task<RiskDto> UpdateRisk(Guid id, SaveRiskRequest request, CancellationToken ct) => _risk.SaveRiskAsync(id, request, ct);

    [HttpGet("risks/{id:guid}/controls")]
    public Task<IReadOnlyList<ControlDto>> Controls(Guid id, CancellationToken ct) => _risk.ListControlsAsync(id, ct);

    [HttpPost("risks/{id:guid}/controls"), HasPermission(Permissions.RiskManage)]
    public Task<ControlDto> AddControl(Guid id, SaveControlRequest request, CancellationToken ct) => _risk.SaveControlAsync(id, null, request, ct);

    [HttpPut("risks/{id:guid}/controls/{controlId:guid}"), HasPermission(Permissions.RiskManage)]
    public Task<ControlDto> UpdateControl(Guid id, Guid controlId, SaveControlRequest request, CancellationToken ct) => _risk.SaveControlAsync(id, controlId, request, ct);

    [HttpPost("risks/{id:guid}/controls/{controlId:guid}/assess"), HasPermission(Permissions.RiskManage, Permissions.AssuranceManage)]
    public Task<ControlDto> AssessControl(Guid id, Guid controlId, AssessControlRequest request, CancellationToken ct) => _risk.AssessControlAsync(id, controlId, request, ct);

    [HttpGet("risks/{id:guid}/treatments")]
    public Task<IReadOnlyList<TreatmentDto>> Treatments(Guid id, CancellationToken ct) => _risk.ListTreatmentsAsync(id, ct);

    [HttpPost("risks/{id:guid}/treatments"), HasPermission(Permissions.RiskManage)]
    public Task<TreatmentDto> AddTreatment(Guid id, SaveTreatmentRequest request, CancellationToken ct) => _risk.SaveTreatmentAsync(id, null, request, ct);

    [HttpPut("risks/{id:guid}/treatments/{treatmentId:guid}"), HasPermission(Permissions.RiskManage)]
    public Task<TreatmentDto> UpdateTreatment(Guid id, Guid treatmentId, SaveTreatmentRequest request, CancellationToken ct) =>
        _risk.SaveTreatmentAsync(id, treatmentId, request, ct);

    [HttpGet("heat-map")]
    public Task<HeatMapDto> HeatMap([FromQuery] Guid? projectId, [FromQuery] bool residual = true, CancellationToken ct = default) => _risk.HeatMapAsync(projectId, residual, ct);

    [HttpGet("obligations")]
    public Task<IReadOnlyList<ObligationDto>> Obligations(CancellationToken ct) => _risk.ListObligationsAsync(ct);

    [HttpPost("obligations"), HasPermission(Permissions.AssuranceManage)]
    public Task<ObligationDto> AddObligation(SaveObligationRequest request, CancellationToken ct) => _risk.SaveObligationAsync(null, request, ct);

    [HttpPut("obligations/{id:guid}"), HasPermission(Permissions.AssuranceManage)]
    public Task<ObligationDto> UpdateObligation(Guid id, SaveObligationRequest request, CancellationToken ct) => _risk.SaveObligationAsync(id, request, ct);

    [HttpGet("attestations")]
    public Task<IReadOnlyList<AttestationDto>> Attestations([FromQuery] Guid? obligationId, [FromQuery] string? period, CancellationToken ct) =>
        _risk.ListAttestationsAsync(obligationId, period, ct);

    [HttpPost("obligations/{id:guid}/attest"), HasPermission(Permissions.ComplianceAttest)]
    public Task<AttestationDto> Attest(Guid id, AttestRequest request, CancellationToken ct) => _risk.AttestAsync(id, request, ct);

    [HttpGet("audit-findings")]
    public Task<IReadOnlyList<AuditFindingDto>> AuditFindings([FromQuery] Guid? projectId, [FromQuery] bool openOnly = false, CancellationToken ct = default) =>
        _risk.ListAuditFindingsAsync(projectId, openOnly, ct);

    [HttpPost("audit-findings"), HasPermission(Permissions.AssuranceManage)]
    public Task<AuditFindingDto> AddAuditFinding(SaveAuditFindingRequest request, CancellationToken ct) => _risk.SaveAuditFindingAsync(null, request, ct);

    [HttpPut("audit-findings/{id:guid}"), HasPermission(Permissions.AssuranceManage)]
    public Task<AuditFindingDto> UpdateAuditFinding(Guid id, SaveAuditFindingRequest request, CancellationToken ct) => _risk.SaveAuditFindingAsync(id, request, ct);

    [HttpGet("coverage")]
    public Task<CoverageMapDto> Coverage([FromQuery] string period, CancellationToken ct) => _risk.CoverageMapAsync(period, ct);

    [HttpPost("coverage"), HasPermission(Permissions.AssuranceManage)]
    public Task<CoverageDto> AddCoverage(SaveCoverageRequest request, CancellationToken ct) => _risk.SaveCoverageAsync(null, request, ct);

    [HttpPut("coverage/{id:guid}"), HasPermission(Permissions.AssuranceManage)]
    public Task<CoverageDto> UpdateCoverage(Guid id, SaveCoverageRequest request, CancellationToken ct) => _risk.SaveCoverageAsync(id, request, ct);
}
