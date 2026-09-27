using Microsoft.AspNetCore.Mvc;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.Reporting;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Report catalogue (RPT-001..015), exports, executive dashboard, board packs, schedules, analytics, geography and data quality (SRS §5.10).</summary>
[Route("api/v1/reports")]
[HasPermission(Permissions.ReportsRead, Permissions.ReportsBoard, Permissions.PortfolioRead)]
public sealed class ReportsController : TetaControllerBase
{
    private readonly IReportingService _reports;

    public ReportsController(IReportingService reports) => _reports = reports;

    [HttpGet("catalogue")]
    public IReadOnlyList<ReportDefinition> Catalogue() => _reports.Catalogue();

    [HttpGet("executive-dashboard")]
    public Task<ExecutiveDashboardDto> Executive([FromQuery] string? financialYear, CancellationToken ct) => _reports.ExecutiveDashboardAsync(financialYear, ct);

    [HttpGet("exceptions")]
    public Task<IReadOnlyList<ExceptionItemDto>> Exceptions([FromQuery] ReportFilter filter, CancellationToken ct) => _reports.ExceptionsAsync(filter, ct);

    [HttpGet("exceptions/export"), HasPermission(Permissions.ReportsExport)]
    public async Task<IActionResult> ExportExceptions([FromQuery] ReportFilter filter, [FromQuery] string format = "xlsx", CancellationToken ct = default) =>
        ExportFile(await _reports.ExportExceptionsAsync(filter, format, ct));

    [HttpGet("run/{code}")]
    public Task<ReportTable> Run(string code, [FromQuery] ReportFilter filter, CancellationToken ct) => _reports.RunAsync(code, filter, ct);

    [HttpGet("run/{code}/export"), HasPermission(Permissions.ReportsExport)]
    public async Task<IActionResult> Export(string code, [FromQuery] ReportFilter filter, [FromQuery] string format = "xlsx", CancellationToken ct = default) =>
        ExportFile(await _reports.ExportAsync(code, filter, format, ct));

    [HttpGet("board-packs"), HasPermission(Permissions.ReportsBoard)]
    public Task<IReadOnlyList<BoardPackDto>> BoardPacks([FromQuery] string? period, CancellationToken ct) => _reports.ListBoardPacksAsync(period, ct);

    [HttpGet("board-packs/{id:guid}"), HasPermission(Permissions.ReportsBoard)]
    public Task<BoardPackDetailDto> BoardPack(Guid id, CancellationToken ct) => _reports.GetBoardPackAsync(id, ct);

    [HttpPost("board-packs"), HasPermission(Permissions.ReportsBoard)]
    public Task<BoardPackDto> GenerateBoardPack(GenerateBoardPackRequest request, CancellationToken ct) => _reports.GenerateBoardPackAsync(request, ct);

    [HttpPost("board-packs/{id:guid}/approve"), HasPermission(Permissions.ReportsBoard)]
    public Task<BoardPackDto> ApproveBoardPack(Guid id, CancellationToken ct) => _reports.ApproveBoardPackAsync(id, ct);

    [HttpGet("schedules"), HasPermission(Permissions.ReportsExport)]
    public Task<IReadOnlyList<ReportScheduleDto>> Schedules(CancellationToken ct) => _reports.ListSchedulesAsync(ct);

    [HttpPost("schedules"), HasPermission(Permissions.ReportsExport)]
    public Task<ReportScheduleDto> CreateSchedule(SaveScheduleRequest request, CancellationToken ct) => _reports.SaveScheduleAsync(null, request, ct);

    [HttpPut("schedules/{id:guid}"), HasPermission(Permissions.ReportsExport)]
    public Task<ReportScheduleDto> UpdateSchedule(Guid id, SaveScheduleRequest request, CancellationToken ct) => _reports.SaveScheduleAsync(id, request, ct);

    [HttpGet("analytics")]
    public Task<ReportTable> Analytics([FromQuery] ReportFilter filter, CancellationToken ct) => _reports.AnalyticsAsync(filter, ct);

    [HttpGet("geographic")]
    public Task<GeoViewDto> Geographic([FromQuery] string level = "Province", [FromQuery] ReportFilter? filter = null, CancellationToken ct = default) =>
        _reports.GeographicAsync(level, filter ?? new ReportFilter(), ct);

    [HttpGet("data-quality")]
    public Task<DataQualityDashboardDto> DataQuality([FromQuery] string? status, [FromQuery] string? ruleCode, CancellationToken ct) =>
        _reports.DataQualityAsync(status, ruleCode, ct);

    [HttpPost("data-quality/{id:guid}/resolve")]
    public Task<DataQualityIssueDto> Resolve(Guid id, ResolveIssueRequest request, CancellationToken ct) => _reports.ResolveIssueAsync(id, request, ct);

    [HttpPost("data-quality/scan"), HasPermission(Permissions.DataQualityManage)]
    public async Task<object> Scan(CancellationToken ct) => new { created = await _reports.ScanDataQualityAsync(ct) };
}
