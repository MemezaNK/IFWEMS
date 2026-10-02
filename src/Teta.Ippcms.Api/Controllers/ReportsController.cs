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
    private readonly ILearnerDeliveryReportService _learnerDelivery;
    private readonly IExecutiveSummaryReportService _executiveSummary;
    private readonly IUserActivityReportService _userActivityReport;

    public ReportsController(IReportingService reports, ILearnerDeliveryReportService learnerDelivery, IExecutiveSummaryReportService executiveSummary,
        IUserActivityReportService userActivityReport)
    {
        _reports = reports;
        _learnerDelivery = learnerDelivery;
        _executiveSummary = executiveSummary;
        _userActivityReport = userActivityReport;
    }

    [HttpGet("catalogue")]
    public IReadOnlyList<ReportDefinition> Catalogue() => _reports.Catalogue();

    [HttpGet("executive-dashboard")]
    public Task<ExecutiveDashboardDto> Executive([FromQuery] string? financialYear, CancellationToken ct) => _reports.ExecutiveDashboardAsync(financialYear, ct);

    [HttpGet("executive-dashboard/export"), HasPermission(Permissions.ReportsExport)]
    public async Task<IActionResult> ExportExecutive([FromQuery] string? financialYear, [FromQuery] string format = "pdf", CancellationToken ct = default) =>
        ExportFile(await _executiveSummary.ExportAsync(financialYear, format, ct));

    /// <summary>
    /// User Activity Report (JSON): a specific user's stats via <paramref name="userId"/>, or the
    /// organisation-wide summary across all active users when <paramref name="userId"/> is omitted.
    /// Gated by the controller's Reports/Board/Portfolio read permissions (admin-level access).
    /// </summary>
    [HttpGet("user-activity")]
    public Task<UserActivityReportDto> UserActivity([FromQuery] Guid? userId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        _userActivityReport.GetAsync(userId, from, to, ct);

    /// <summary>Client-ready PDF export of the User Activity Report (a specific user, or the organisation-wide summary).</summary>
    [HttpGet("user-activity/export"), HasPermission(Permissions.ReportsExport)]
    public async Task<IActionResult> ExportUserActivity([FromQuery] Guid? userId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string format = "pdf", CancellationToken ct = default) =>
        ExportFile(await _userActivityReport.ExportAsync(userId, from, to, format, ct));

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

    [HttpGet("board-packs/{id:guid}/export"), HasPermission(Permissions.ReportsExport)]
    public async Task<IActionResult> ExportBoardPack(Guid id, CancellationToken ct) => ExportFile(await _reports.ExportBoardPackAsync(id, ct));

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

    // ----- Learner Delivery & Monitoring Report (project-scoped interactive dashboard + export) -----

    [HttpGet("learner-delivery/{projectId:guid}"), HasPermission(Permissions.MeRead)]
    public Task<LearnerDeliveryDashboardDto> LearnerDelivery(Guid projectId, CancellationToken ct) =>
        _learnerDelivery.GetDashboardAsync(projectId, ct);

    [HttpGet("learner-delivery/{projectId:guid}/export"), HasPermission(Permissions.ReportsExport)]
    public async Task<IActionResult> ExportLearnerDelivery(Guid projectId, [FromQuery] string format = "pdf", CancellationToken ct = default) =>
        ExportFile(await _learnerDelivery.ExportAsync(projectId, format, ct));

    [HttpGet("learner-delivery/{projectId:guid}/target"), HasPermission(Permissions.MeRead)]
    public Task<LearnerDeliveryTargetDto> LearnerDeliveryTarget(Guid projectId, CancellationToken ct) =>
        _learnerDelivery.GetTargetAsync(projectId, ct);

    [HttpPut("learner-delivery/{projectId:guid}/target"), HasPermission(Permissions.MeManage)]
    public Task<LearnerDeliveryTargetDto> SaveLearnerDeliveryTarget(Guid projectId, SaveLearnerDeliveryTargetRequest request, CancellationToken ct) =>
        _learnerDelivery.SaveTargetAsync(projectId, request, ct);
}
