using Microsoft.AspNetCore.Mvc;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.Finance;
using Teta.Ippcms.Application.Portal;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Supplier / implementing-partner portal: own bids, contracts, deliverables and invoices only (SRS §13).</summary>
[Route("api/v1/portal")]
[HasPermission(Permissions.PortalAccess)]
public sealed class PortalController : TetaControllerBase
{
    private readonly IPortalService _portal;

    public PortalController(IPortalService portal) => _portal = portal;

    [HttpGet("summary")]
    public Task<PortalSummaryDto> Summary(CancellationToken ct) => _portal.SummaryAsync(ct);

    [HttpGet("bids")]
    public Task<IReadOnlyList<PortalBidDto>> Bids(CancellationToken ct) => _portal.MyBidsAsync(ct);

    [HttpGet("contracts")]
    public Task<IReadOnlyList<PortalContractDto>> Contracts(CancellationToken ct) => _portal.MyContractsAsync(ct);

    [HttpGet("deliverables")]
    public Task<IReadOnlyList<PortalDeliverableDto>> Deliverables([FromQuery] Guid? contractId, CancellationToken ct) => _portal.MyDeliverablesAsync(contractId, ct);

    [HttpPost("deliverables/{id:guid}/submit")]
    public Task<PortalDeliverableDto> SubmitDeliverable(Guid id, SubmitDeliverableRequest request, CancellationToken ct) => _portal.SubmitDeliverableAsync(id, request, ct);

    [HttpGet("invoices")]
    public Task<IReadOnlyList<PortalInvoiceDto>> Invoices(CancellationToken ct) => _portal.MyInvoicesAsync(ct);

    [HttpPost("invoices")]
    public Task<PortalInvoiceDto> SubmitInvoice(RegisterInvoiceRequest request, CancellationToken ct) => _portal.SubmitInvoiceAsync(request, IdempotencyKey, ct);
}
