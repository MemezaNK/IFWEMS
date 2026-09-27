using Microsoft.AspNetCore.Mvc;
using Platform.Core;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.ContractManagement;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Contract register, obligations, deliverables, payment schedules, variations, performance and close-out (SRS §5.6).</summary>
[Route("api/v1/contracts")]
[HasPermission(Permissions.ContractRead)]
public sealed class ContractsController : TetaControllerBase
{
    private readonly IContractService _contracts;

    public ContractsController(IContractService contracts) => _contracts = contracts;

    [HttpGet]
    public Task<PagedResult<ContractListItemDto>> List([FromQuery] ContractQuery query, CancellationToken ct) => _contracts.ListAsync(query, ct);

    [HttpGet("dashboard")]
    public Task<ContractDashboardDto> Dashboard([FromQuery] Guid? projectId, CancellationToken ct) => _contracts.DashboardAsync(projectId, ct);

    [HttpGet("{id:guid}")]
    public Task<ContractDetailDto> Get(Guid id, CancellationToken ct) => _contracts.GetAsync(id, ct);

    [HttpPost("from-award"), HasPermission(Permissions.ContractManage)]
    public Task<ContractDetailDto> FromAward(CreateContractFromAwardRequest request, CancellationToken ct) => _contracts.CreateFromAwardAsync(request, ct);

    [HttpPost("non-bid"), HasPermission(Permissions.ContractManage)]
    public Task<ContractDetailDto> NonBid(CreateNonBidContractRequest request, CancellationToken ct) => _contracts.CreateNonBidAsync(request, ct);

    [HttpPut("{id:guid}"), HasPermission(Permissions.ContractManage)]
    public Task<ContractDetailDto> Update(Guid id, UpdateContractRequest request, CancellationToken ct) => _contracts.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/sign"), HasPermission(Permissions.ContractManage, Permissions.ContractApprove)]
    public Task<ContractDetailDto> Sign(Guid id, SignContractRequest request, CancellationToken ct) => _contracts.SignAsync(id, request, ct);

    [HttpGet("{id:guid}/committed")]
    public async Task<object> Committed(Guid id, CancellationToken ct) => new { committed = await _contracts.CommittedAsync(id, ct) };

    [HttpPost("{id:guid}/obligations"), HasPermission(Permissions.ContractManage)]
    public Task<ObligationDto> AddObligation(Guid id, SaveObligationRequest request, CancellationToken ct) => _contracts.SaveObligationAsync(id, null, request, ct);

    [HttpPut("{id:guid}/obligations/{obligationId:guid}"), HasPermission(Permissions.ContractManage)]
    public Task<ObligationDto> UpdateObligation(Guid id, Guid obligationId, SaveObligationRequest request, CancellationToken ct) =>
        _contracts.SaveObligationAsync(id, obligationId, request, ct);

    [HttpGet("deliverables")]
    public Task<IReadOnlyList<DeliverableDto>> Deliverables([FromQuery] Guid? projectId, [FromQuery] Guid? contractId, CancellationToken ct) =>
        _contracts.ListDeliverablesAsync(projectId, contractId, ct);

    [HttpPost("deliverables"), HasPermission(Permissions.ContractManage, Permissions.ExecutionManage)]
    public Task<DeliverableDto> AddDeliverable(SaveDeliverableRequest request, CancellationToken ct) => _contracts.SaveDeliverableAsync(null, request, ct);

    [HttpPut("deliverables/{deliverableId:guid}"), HasPermission(Permissions.ContractManage, Permissions.ExecutionManage)]
    public Task<DeliverableDto> UpdateDeliverable(Guid deliverableId, SaveDeliverableRequest request, CancellationToken ct) =>
        _contracts.SaveDeliverableAsync(deliverableId, request, ct);

    [HttpPost("deliverables/{deliverableId:guid}/submit"), HasPermission(Permissions.ContractManage, Permissions.ExecutionManage)]
    public Task<DeliverableDto> SubmitDeliverable(Guid deliverableId, CancellationToken ct) => _contracts.SubmitDeliverableAsync(deliverableId, ct);

    [HttpPost("deliverables/{deliverableId:guid}/decision"), HasPermission(Permissions.ContractManage, Permissions.ExecutionManage)]
    public Task<DeliverableDto> DecideDeliverable(Guid deliverableId, DeliverableDecisionRequest request, CancellationToken ct) =>
        _contracts.DecideDeliverableAsync(deliverableId, request, ct);

    [HttpPost("{id:guid}/payment-schedule"), HasPermission(Permissions.ContractManage)]
    public Task<PaymentScheduleDto> AddPaymentItem(Guid id, SavePaymentScheduleRequest request, CancellationToken ct) => _contracts.SavePaymentScheduleAsync(id, null, request, ct);

    [HttpPut("{id:guid}/payment-schedule/{itemId:guid}"), HasPermission(Permissions.ContractManage)]
    public Task<PaymentScheduleDto> UpdatePaymentItem(Guid id, Guid itemId, SavePaymentScheduleRequest request, CancellationToken ct) =>
        _contracts.SavePaymentScheduleAsync(id, itemId, request, ct);

    [HttpPost("{id:guid}/variations"), HasPermission(Permissions.ContractManage)]
    public Task<VariationDto> AddVariation(Guid id, SaveVariationRequest request, CancellationToken ct) => _contracts.SaveVariationAsync(id, null, request, ct);

    [HttpPut("{id:guid}/variations/{variationId:guid}"), HasPermission(Permissions.ContractManage)]
    public Task<VariationDto> UpdateVariation(Guid id, Guid variationId, SaveVariationRequest request, CancellationToken ct) =>
        _contracts.SaveVariationAsync(id, variationId, request, ct);

    [HttpPost("{id:guid}/variations/{variationId:guid}/submit"), HasPermission(Permissions.ContractManage)]
    public Task<WorkflowInstanceDto> SubmitVariation(Guid id, Guid variationId, CancellationToken ct) => _contracts.SubmitVariationAsync(id, variationId, ct);

    [HttpPost("{id:guid}/reviews"), HasPermission(Permissions.ContractManage)]
    public Task<ReviewDto> AddReview(Guid id, SaveReviewRequest request, CancellationToken ct) => _contracts.AddReviewAsync(id, request, ct);

    [HttpPost("{id:guid}/breaches"), HasPermission(Permissions.ContractManage)]
    public Task<BreachDto> AddBreach(Guid id, SaveBreachRequest request, CancellationToken ct) => _contracts.SaveBreachAsync(id, null, request, ct);

    [HttpPut("{id:guid}/breaches/{breachId:guid}"), HasPermission(Permissions.ContractManage)]
    public Task<BreachDto> UpdateBreach(Guid id, Guid breachId, SaveBreachRequest request, CancellationToken ct) => _contracts.SaveBreachAsync(id, breachId, request, ct);

    [HttpGet("{id:guid}/close-out-checks")]
    public Task<CloseOutResultDto> CloseOutChecks(Guid id, CancellationToken ct) => _contracts.CloseOutChecksAsync(id, ct);

    [HttpPost("{id:guid}/close"), HasPermission(Permissions.ContractManage)]
    public Task<ContractDetailDto> Close(Guid id, CloseContractRequest request, CancellationToken ct) => _contracts.CloseAsync(id, request, ct);
}
