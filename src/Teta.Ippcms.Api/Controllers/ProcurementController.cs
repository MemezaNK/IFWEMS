using Microsoft.AspNetCore.Mvc;
using Platform.Core;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.Budget;
using Teta.Ippcms.Application.Sourcing;
using Teta.Ippcms.Application.Suppliers;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Security;
using ProcurementCriterionDto = Teta.Ippcms.Application.Sourcing.CriterionDto;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Procurement planning, requisitions, sourcing, evaluation, adjudication and awards (SRS §5.4-5.5).</summary>
[Route("api/v1/procurement")]
public sealed class ProcurementController : TetaControllerBase
{
    private readonly IProcurementService _procurement;
    private readonly IBudgetService _budget;

    public ProcurementController(IProcurementService procurement, IBudgetService budget)
    {
        _procurement = procurement;
        _budget = budget;
    }

    public sealed record NoteRequest(string Note);

    // ----- Procurement plan & method rules -----
    [HttpGet("plan"), HasPermission(Permissions.ProcurementRead, Permissions.BudgetManage)]
    public Task<IReadOnlyList<PlanItemDto>> Plan([FromQuery] string? financialYear, [FromQuery] Guid? projectId, CancellationToken ct) =>
        _budget.ListPlanAsync(financialYear, projectId, ct);

    [HttpPost("plan"), HasPermission(Permissions.ProcurementManage, Permissions.BudgetManage)]
    public Task<PlanItemDto> AddPlanItem(SavePlanItemRequest request, CancellationToken ct) => _budget.SavePlanItemAsync(null, request, ct);

    [HttpPut("plan/{id:guid}"), HasPermission(Permissions.ProcurementManage, Permissions.BudgetManage)]
    public Task<PlanItemDto> UpdatePlanItem(Guid id, SavePlanItemRequest request, CancellationToken ct) => _budget.SavePlanItemAsync(id, request, ct);

    [HttpPost("plan/generate"), HasPermission(Permissions.ProcurementManage)]
    public async Task<object> GenerateDemandPlan([FromQuery] string financialYear, CancellationToken ct) =>
        new { created = await _budget.GenerateDemandPlanAsync(financialYear, ct) };

    [HttpGet("plan/commitment-forecast"), HasPermission(Permissions.ProcurementRead, Permissions.FinanceRead)]
    public Task<IReadOnlyList<CommitmentForecastDto>> CommitmentForecast([FromQuery] string financialYear, CancellationToken ct) =>
        _budget.CommitmentForecastAsync(financialYear, ct);

    [HttpGet("method-rules"), HasPermission(Permissions.ProcurementRead)]
    public Task<IReadOnlyList<MethodRuleDto>> Rules(CancellationToken ct) => _budget.ListRulesAsync(ct);

    [HttpPost("method-rules"), HasPermission(Permissions.ProcurementManage)]
    public Task<MethodRuleDto> CreateRule(SaveMethodRuleRequest request, CancellationToken ct) => _budget.SaveRuleAsync(null, request, ct);

    [HttpPut("method-rules/{id:guid}"), HasPermission(Permissions.ProcurementManage)]
    public Task<MethodRuleDto> UpdateRule(Guid id, SaveMethodRuleRequest request, CancellationToken ct) => _budget.SaveRuleAsync(id, request, ct);

    [HttpPost("method-rules/{id:guid}/submit"), HasPermission(Permissions.ProcurementManage)]
    public Task<MethodRuleDto> SubmitRule(Guid id, CancellationToken ct) => _budget.SubmitRuleAsync(id, ct);

    [HttpPost("method-rules/{id:guid}/decision"), HasPermission(Permissions.AdminConfigApprove)]
    public Task<MethodRuleDto> DecideRule(Guid id, DecisionRequest request, CancellationToken ct) => _budget.ApproveRuleAsync(id, request.Approve, request.Comment, ct);

    [HttpGet("method-rules/select"), HasPermission(Permissions.ProcurementRead, Permissions.ProjectManage)]
    public Task<MethodSelection> SelectMethod([FromQuery] decimal value, [FromQuery] DateOnly? onDate, CancellationToken ct) => _budget.SelectMethodAsync(value, onDate, ct);

    // ----- Requisitions -----
    [HttpGet("requisitions"), HasPermission(Permissions.ProcurementRead)]
    public Task<IReadOnlyList<RequisitionDto>> Requisitions([FromQuery] Guid? projectId, [FromQuery] string? status, CancellationToken ct) =>
        _procurement.ListRequisitionsAsync(projectId, status, ct);

    [HttpGet("requisitions/{id:guid}"), HasPermission(Permissions.ProcurementRead)]
    public Task<RequisitionDto> Requisition(Guid id, CancellationToken ct) => _procurement.GetRequisitionAsync(id, ct);

    [HttpPost("requisitions"), HasPermission(Permissions.ProjectManage, Permissions.ProcurementManage)]
    public Task<RequisitionDto> CreateRequisition(SaveRequisitionRequest request, CancellationToken ct) => _procurement.SaveRequisitionAsync(null, request, ct);

    [HttpPut("requisitions/{id:guid}"), HasPermission(Permissions.ProjectManage, Permissions.ProcurementManage)]
    public Task<RequisitionDto> UpdateRequisition(Guid id, SaveRequisitionRequest request, CancellationToken ct) => _procurement.SaveRequisitionAsync(id, request, ct);

    [HttpPost("requisitions/{id:guid}/submit"), HasPermission(Permissions.ProjectManage, Permissions.ProcurementManage)]
    public Task<WorkflowInstanceDto> SubmitRequisition(Guid id, CancellationToken ct) => _procurement.SubmitRequisitionAsync(id, ct);

    [HttpPost("requisitions/{id:guid}/convert"), HasPermission(Permissions.ProcurementManage)]
    public Task<ProcurementDetailDto> Convert(Guid id, CancellationToken ct) => _procurement.ConvertToProcurementAsync(id, ct);

    // ----- Procurements -----
    [HttpGet, HasPermission(Permissions.ProcurementRead, Permissions.ProcurementEvaluate)]
    public Task<PagedResult<ProcurementListItemDto>> List([FromQuery] ProcurementQuery query, CancellationToken ct) => _procurement.ListAsync(query, ct);

    [HttpGet("dashboard"), HasPermission(Permissions.ProcurementRead)]
    public Task<ProcurementDashboardDto> Dashboard([FromQuery] Guid? projectId, [FromQuery] string? orgUnit, [FromQuery] string? method, [FromQuery] string? status,
        CancellationToken ct) => _procurement.DashboardAsync(projectId, orgUnit, method, status, ct);

    [HttpGet("transparency"), HasPermission(Permissions.ProcurementRead)]
    public Task<IReadOnlyList<TransparencyRow>> Transparency([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        _procurement.TransparencyAsync(from, to, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.ProcurementRead, Permissions.ProcurementEvaluate)]
    public Task<ProcurementDetailDto> Get(Guid id, CancellationToken ct) => _procurement.GetAsync(id, ct);

    [HttpPost("{id:guid}/specifications"), HasPermission(Permissions.ProcurementManage, Permissions.ProjectManage)]
    public Task<SpecificationDto> SaveSpecification(Guid id, SaveSpecificationRequest request, CancellationToken ct) => _procurement.SaveSpecificationAsync(id, request, ct);

    [HttpPost("{id:guid}/specifications/{specId:guid}/submit"), HasPermission(Permissions.ProcurementManage, Permissions.ProjectManage)]
    public Task<SpecificationDto> SubmitSpecification(Guid id, Guid specId, CancellationToken ct) => _procurement.SubmitSpecificationAsync(id, specId, ct);

    [HttpPost("{id:guid}/specifications/{specId:guid}/approve"), HasPermission(Permissions.ProcurementManage)]
    public Task<SpecificationDto> ApproveSpecification(Guid id, Guid specId, CancellationToken ct) => _procurement.ApproveSpecificationAsync(id, specId, ct);

    [HttpPost("{id:guid}/committees"), HasPermission(Permissions.ProcurementManage)]
    public Task<CommitteeDto> SaveCommittee(Guid id, SaveCommitteeRequest request, CancellationToken ct) => _procurement.SaveCommitteeAsync(id, request, ct);

    [HttpPost("committees/{committeeId:guid}/members"), HasPermission(Permissions.ProcurementManage)]
    public Task<CommitteeDto> AddMember(Guid committeeId, AddMemberRequest request, CancellationToken ct) => _procurement.AddMemberAsync(committeeId, request, ct);

    [HttpPost("committees/{committeeId:guid}/members/{memberId:guid}/end"), HasPermission(Permissions.ProcurementManage)]
    public Task<CommitteeDto> EndMembership(Guid committeeId, Guid memberId, CancellationToken ct) => _procurement.EndMembershipAsync(committeeId, memberId, ct);

    [HttpGet("committees/{committeeId:guid}/meetings"), HasPermission(Permissions.ProcurementRead, Permissions.ProcurementEvaluate)]
    public Task<IReadOnlyList<MeetingDto>> Meetings(Guid committeeId, CancellationToken ct) => _procurement.ListMeetingsAsync(committeeId, ct);

    [HttpPost("committees/{committeeId:guid}/meetings"), HasPermission(Permissions.ProcurementManage)]
    public Task<MeetingDto> AddMeeting(Guid committeeId, SaveMeetingRequest request, CancellationToken ct) => _procurement.AddMeetingAsync(committeeId, request, ct);

    [HttpGet("{id:guid}/declarations"), HasPermission(Permissions.ProcurementRead, Permissions.ProcurementEvaluate)]
    public Task<IReadOnlyList<DeclarationDto>> Declarations(Guid id, CancellationToken ct) => _procurement.ListDeclarationsAsync(id, ct);

    [HttpPost("{id:guid}/declarations"), HasPermission(Permissions.ProcurementEvaluate, Permissions.ProcurementManage)]
    public Task<DeclarationDto> Declare(Guid id, DeclareRequest request, CancellationToken ct) => _procurement.DeclareAsync(id, request, ct);

    [HttpPost("{id:guid}/publications"), HasPermission(Permissions.ProcurementManage)]
    public Task<PublicationDto> Publish(Guid id, SavePublicationRequest request, CancellationToken ct) => _procurement.PublishAsync(id, request, ct);

    [HttpGet("{id:guid}/bids"), HasPermission(Permissions.ProcurementManage, Permissions.ProcurementEvaluate)]
    public Task<IReadOnlyList<BidDto>> Bids(Guid id, CancellationToken ct) => _procurement.ListBidsAsync(id, ct);

    [HttpPost("{id:guid}/bids"), HasPermission(Permissions.ProcurementManage)]
    public Task<BidDto> RegisterBid(Guid id, RegisterBidRequest request, CancellationToken ct) => _procurement.RegisterBidAsync(id, request, ct);

    [HttpPost("{id:guid}/bids/open"), HasPermission(Permissions.ProcurementManage)]
    public Task<IReadOnlyList<BidDto>> OpenBids(Guid id, CancellationToken ct) => _procurement.OpenBidsAsync(id, ct);

    [HttpPost("{id:guid}/bids/{bidId:guid}/invalidate"), HasPermission(Permissions.ProcurementManage, Permissions.ProcurementEvaluate)]
    public Task<BidDto> InvalidateBid(Guid id, Guid bidId, ReasonRequest request, CancellationToken ct) => _procurement.InvalidateBidAsync(id, bidId, request.Reason, ct);

    [HttpPost("{id:guid}/criteria"), HasPermission(Permissions.ProcurementManage)]
    public Task<ProcurementCriterionDto> AddCriterion(Guid id, SaveCriterionRequest request, CancellationToken ct) => _procurement.SaveCriterionAsync(id, null, request, ct);

    [HttpPut("{id:guid}/criteria/{criterionId:guid}"), HasPermission(Permissions.ProcurementManage)]
    public Task<ProcurementCriterionDto> UpdateCriterion(Guid id, Guid criterionId, SaveCriterionRequest request, CancellationToken ct) =>
        _procurement.SaveCriterionAsync(id, criterionId, request, ct);

    [HttpDelete("{id:guid}/criteria/{criterionId:guid}"), HasPermission(Permissions.ProcurementManage)]
    public async Task<IActionResult> DeleteCriterion(Guid id, Guid criterionId, CancellationToken ct)
    {
        await _procurement.DeleteCriterionAsync(id, criterionId, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/evaluation-setup"), HasPermission(Permissions.ProcurementManage)]
    public async Task<IActionResult> SetupEvaluation(Guid id, EvaluationSetupRequest request, CancellationToken ct)
    {
        await _procurement.SetupEvaluationAsync(id, request, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/my-scores"), HasPermission(Permissions.ProcurementEvaluate)]
    public Task<IReadOnlyList<ScoreDto>> MyScores(Guid id, CancellationToken ct) => _procurement.MyScoresAsync(id, ct);

    [HttpPost("{id:guid}/scores"), HasPermission(Permissions.ProcurementEvaluate)]
    public Task<ScoreDto> SubmitScore(Guid id, SubmitScoreRequest request, CancellationToken ct) => _procurement.SubmitScoreAsync(id, request, ct);

    [HttpPost("{id:guid}/consolidate"), HasPermission(Permissions.ProcurementManage)]
    public Task<EvaluationReportDto> Consolidate(Guid id, CancellationToken ct) => _procurement.ConsolidateAsync(id, ct);

    [HttpGet("{id:guid}/evaluation-report"), HasPermission(Permissions.ProcurementManage, Permissions.ProcurementAdjudicate, Permissions.ProcurementEvaluate)]
    public Task<EvaluationReportDto> EvaluationReport(Guid id, CancellationToken ct) => _procurement.EvaluationReportAsync(id, ct);

    [HttpGet("{id:guid}/due-diligence"), HasPermission(Permissions.ProcurementManage, Permissions.ProcurementAdjudicate)]
    public Task<IReadOnlyList<DueDiligenceDto>> DueDiligence(Guid id, CancellationToken ct) => _procurement.ListDueDiligenceAsync(id, ct);

    [HttpPost("{id:guid}/due-diligence"), HasPermission(Permissions.ProcurementManage)]
    public Task<DueDiligenceDto> SaveDueDiligence(Guid id, SaveDueDiligenceRequest request, CancellationToken ct) => _procurement.SaveDueDiligenceAsync(id, request, ct);

    [HttpPost("{id:guid}/adjudication"), HasPermission(Permissions.ProcurementManage)]
    public Task<WorkflowInstanceDto> SubmitAdjudication(Guid id, SubmitAdjudicationRequest request, CancellationToken ct) =>
        _procurement.SubmitAdjudicationAsync(id, request, ct);

    [HttpGet("awards"), HasPermission(Permissions.ProcurementRead, Permissions.ContractManage)]
    public Task<IReadOnlyList<AwardDto>> Awards([FromQuery] bool pendingContractOnly = false, CancellationToken ct = default) =>
        _procurement.ListAwardsAsync(pendingContractOnly, ct);

    [HttpPost("awards/{awardId:guid}/conditions-satisfied"), HasPermission(Permissions.ProcurementAward, Permissions.ProcurementManage)]
    public Task<AwardDto> SatisfyConditions(Guid awardId, NoteRequest request, CancellationToken ct) => _procurement.SatisfyAwardConditionsAsync(awardId, request.Note, ct);

    [HttpPost("{id:guid}/cancel"), HasPermission(Permissions.ProcurementManage)]
    public Task<ProcurementDetailDto> Cancel(Guid id, ReasonRequest request, CancellationToken ct) => _procurement.CancelAsync(id, request.Reason, ct);

    [HttpPost("{id:guid}/re-advertise"), HasPermission(Permissions.ProcurementManage)]
    public Task<ProcurementDetailDto> ReAdvertise(Guid id, ReasonRequest request, CancellationToken ct) => _procurement.ReAdvertiseAsync(id, request.Reason, ct);

    [HttpGet("{id:guid}/communications"), HasPermission(Permissions.ProcurementRead)]
    public Task<IReadOnlyList<CommunicationDto>> Communications(Guid id, CancellationToken ct) => _procurement.ListCommunicationsAsync(id, ct);

    [HttpPost("{id:guid}/communications"), HasPermission(Permissions.ProcurementManage)]
    public Task<CommunicationDto> AddCommunication(Guid id, SaveCommunicationRequest request, CancellationToken ct) => _procurement.AddCommunicationAsync(id, request, ct);

    // ----- Deviations / exceptions -----
    [HttpGet("exceptions"), HasPermission(Permissions.ProcurementRead)]
    public Task<IReadOnlyList<ExceptionDto>> Exceptions([FromQuery] string? status, CancellationToken ct) => _procurement.ListExceptionsAsync(status, ct);

    [HttpPost("exceptions"), HasPermission(Permissions.ProcurementManage)]
    public Task<ExceptionDto> CreateException(SaveExceptionRequest request, CancellationToken ct) => _procurement.SaveExceptionAsync(null, request, ct);

    [HttpPut("exceptions/{id:guid}"), HasPermission(Permissions.ProcurementManage)]
    public Task<ExceptionDto> UpdateException(Guid id, SaveExceptionRequest request, CancellationToken ct) => _procurement.SaveExceptionAsync(id, request, ct);

    [HttpPost("exceptions/{id:guid}/submit"), HasPermission(Permissions.ProcurementManage)]
    public Task<WorkflowInstanceDto> SubmitException(Guid id, CancellationToken ct) => _procurement.SubmitExceptionAsync(id, ct);
}

/// <summary>Supplier / implementing-partner master data with duplicate control (FR-SCM-018).</summary>
[Route("api/v1/suppliers")]
[HasPermission(Permissions.SupplierRead)]
public sealed class SuppliersController : TetaControllerBase
{
    private readonly ISupplierService _suppliers;

    public SuppliersController(ISupplierService suppliers) => _suppliers = suppliers;

    public sealed record LinkUserRequest(Guid UserId);

    [HttpGet]
    public Task<PagedResult<SupplierDto>> List([FromQuery] string? search, [FromQuery] string? status, [FromQuery] bool? implementingPartner,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        _suppliers.ListAsync(search, status, implementingPartner, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<SupplierDto> Get(Guid id, CancellationToken ct) => _suppliers.GetAsync(id, ct);

    [HttpPost("duplicates"), HasPermission(Permissions.SupplierManage)]
    public Task<IReadOnlyList<DuplicateCandidate>> Duplicates(SaveSupplierRequest request, [FromQuery] Guid? excludeId, CancellationToken ct) =>
        _suppliers.FindDuplicatesAsync(request, excludeId, ct);

    [HttpPost, HasPermission(Permissions.SupplierManage)]
    public Task<SupplierDto> Create(SaveSupplierRequest request, CancellationToken ct) => _suppliers.SaveAsync(null, request, ct);

    [HttpPut("{id:guid}"), HasPermission(Permissions.SupplierManage)]
    public Task<SupplierDto> Update(Guid id, SaveSupplierRequest request, CancellationToken ct) => _suppliers.SaveAsync(id, request, ct);

    [HttpGet("{id:guid}/history")]
    public Task<SupplierHistoryDto> History(Guid id, CancellationToken ct) => _suppliers.HistoryAsync(id, ct);

    [HttpPost("{id:guid}/portal-users"), HasPermission(Permissions.SupplierManage, Permissions.SecurityUsers)]
    public async Task<IActionResult> LinkPortalUser(Guid id, LinkUserRequest request, CancellationToken ct)
    {
        await _suppliers.LinkPortalUserAsync(id, request.UserId, ct);
        return NoContent();
    }
}
