using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Core;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.Finance;
using Teta.Ippcms.Application.Workflow;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Financial views, commitments, invoice registration/three-way check/certification, accruals and forecasts (SRS §5.7).</summary>
[Route("api/v1/finance")]
[HasPermission(Permissions.FinanceRead)]
public sealed class FinanceController : TetaControllerBase
{
    private readonly IFinanceService _finance;

    public FinanceController(IFinanceService finance) => _finance = finance;

    [HttpGet("dashboard")]
    public Task<FinanceDashboardDto> Dashboard([FromQuery] string? financialYear, CancellationToken ct) => _finance.DashboardAsync(financialYear, ct);

    [HttpGet("portfolio")]
    public Task<FinancialViewDto> Portfolio([FromQuery] string? financialYear, CancellationToken ct) => _finance.PortfolioViewAsync(financialYear, ct);

    [HttpGet("projects/{projectId:guid}")]
    public Task<FinancialViewDto> Project(Guid projectId, CancellationToken ct) => _finance.ProjectViewAsync(projectId, ct);

    [HttpGet("projects/{projectId:guid}/commitments")]
    public Task<IReadOnlyList<CommitmentDto>> Commitments(Guid projectId, CancellationToken ct) => _finance.ListCommitmentsAsync(projectId, ct);

    [HttpPost("commitments"), HasPermission(Permissions.FinanceManage)]
    public Task<CommitmentDto> AddCommitment(SaveCommitmentRequest request, CancellationToken ct) => _finance.AddCommitmentAsync(request, ct);

    [HttpGet("invoices")]
    public Task<PagedResult<InvoiceDto>> Invoices([FromQuery] Guid? projectId, [FromQuery] Guid? contractId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        _finance.ListInvoicesAsync(projectId, contractId, status, page, pageSize, ct);

    [HttpGet("invoices/{id:guid}")]
    public Task<InvoiceDto> Invoice(Guid id, CancellationToken ct) => _finance.GetInvoiceAsync(id, ct);

    [HttpPost("invoices"), HasPermission(Permissions.FinanceManage)]
    public Task<InvoiceDto> Register(RegisterInvoiceRequest request, CancellationToken ct) => _finance.RegisterInvoiceAsync(request, IdempotencyKey, ct);

    [HttpPost("invoices/{id:guid}/validate"), HasPermission(Permissions.FinanceManage)]
    public Task<InvoiceDto> Validate(Guid id, CancellationToken ct) => _finance.ValidateInvoiceAsync(id, ct);

    [HttpPost("invoices/{id:guid}/submit"), HasPermission(Permissions.FinanceManage)]
    public Task<WorkflowInstanceDto> Submit(Guid id, CancellationToken ct) => _finance.SubmitForCertificationAsync(id, ct);

    [HttpPost("invoices/{id:guid}/reject"), HasPermission(Permissions.FinanceManage)]
    public Task<InvoiceDto> Reject(Guid id, ReasonRequest request, CancellationToken ct) => _finance.RejectInvoiceAsync(id, request.Reason, ct);

    [HttpGet("projects/{projectId:guid}/accruals")]
    public Task<IReadOnlyList<AccrualDto>> Accruals(Guid projectId, CancellationToken ct) => _finance.ListAccrualsAsync(projectId, ct);

    [HttpPost("accruals"), HasPermission(Permissions.FinanceManage)]
    public Task<AccrualDto> AddAccrual(SaveAccrualRequest request, CancellationToken ct) => _finance.AddAccrualAsync(request, ct);

    [HttpGet("projects/{projectId:guid}/forecasts")]
    public Task<IReadOnlyList<ForecastDto>> Forecasts(Guid projectId, CancellationToken ct) => _finance.ListForecastsAsync(projectId, ct);

    [HttpPost("projects/{projectId:guid}/forecasts"), HasPermission(Permissions.FinanceManage, Permissions.ExecutionManage)]
    public Task<ForecastDto> RecordForecast(Guid projectId, SaveForecastRequest request, CancellationToken ct) => _finance.RecordForecastAsync(projectId, request, ct);

    [HttpGet("erp/messages"), HasPermission(Permissions.FinanceErp)]
    public Task<IReadOnlyList<ErpMessageDto>> ErpMessages([FromQuery] string? status, CancellationToken ct) => _finance.ListErpMessagesAsync(status, ct);

    [HttpGet("erp/reconciliations"), HasPermission(Permissions.FinanceErp)]
    public Task<IReadOnlyList<ReconciliationDto>> Reconciliations(CancellationToken ct) => _finance.ListReconciliationsAsync(ct);

    [HttpPost("erp/retry"), HasPermission(Permissions.FinanceErp)]
    public Task<ErpBatchResult> Retry(CancellationToken ct) => _finance.RetryErpErrorsAsync(ct);
}

/// <summary>
/// Inbound ERP interface (FR-FIN-002): payments, expenditure and commitments arrive as controlled
/// batches with control totals. Authenticated by user token (finance.erp) or the ERP API key.
/// </summary>
[Route("api/v1/integration/erp")]
[Authorize(AuthenticationSchemes = "Bearer," + ErpApiKeyAuthentication.Scheme)]
[HasPermission(Permissions.FinanceErp)]
public sealed class ErpIntegrationController : TetaControllerBase
{
    private readonly IFinanceService _finance;

    public ErpIntegrationController(IFinanceService finance) => _finance = finance;

    [HttpPost("batches")]
    public Task<ErpBatchResult> Receive(ErpBatchRequest request, CancellationToken ct) => _finance.ReceiveErpBatchAsync(request, ct);
}
