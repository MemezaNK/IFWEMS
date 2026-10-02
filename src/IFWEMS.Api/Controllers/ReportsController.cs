using System.Globalization;
using System.Security.Claims;
using System.Text;
using IFWEMS.Application.Common.Interfaces;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

/// <summary>
/// Permission-scoped registers/reports, exportable to CSV (Excel-compatible). FR-042.
/// PDF/Excel-native export can be layered on later; CSV satisfies the exportable-register
/// requirement and opens directly in Excel.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;
    private readonly IUserActivityReportService _userActivityReportService;

    public ReportsController(IfwemsDbContext dbContext, IUserActivityReportService userActivityReportService)
    {
        _dbContext = dbContext;
        _userActivityReportService = userActivityReportService;
    }

    private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private bool IsAdmin => User.IsInRole("SystemAdministrator") || User.IsInRole("ComplianceOfficer");

    /// <summary>
    /// User Activity Report (JSON): the signed-in user's own stats, or (SystemAdministrator/
    /// ComplianceOfficer only) another user's stats via <paramref name="userId"/>, or the
    /// organisation-wide summary when <paramref name="userId"/> is omitted and <paramref name="all"/> is true.
    /// </summary>
    [HttpGet("user-activity")]
    public async Task<IActionResult> UserActivity([FromQuery] Guid? userId, [FromQuery] bool all, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if ((all || (userId is { } requested && requested != CurrentUserId)) && !IsAdmin)
        {
            return Forbid();
        }

        var targetUserId = all ? null : userId ?? CurrentUserId;
        if (targetUserId is null && !all)
        {
            return Unauthorized();
        }

        return Ok(await _userActivityReportService.GetAsync(targetUserId, from, to, cancellationToken));
    }

    /// <summary>Client-ready PDF export of the User Activity Report (own activity, or any user's / org-wide for admins).</summary>
    [HttpGet("user-activity.pdf")]
    public async Task<IActionResult> UserActivityPdf([FromQuery] Guid? userId, [FromQuery] bool all, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if ((all || (userId is { } requested && requested != CurrentUserId)) && !IsAdmin)
        {
            return Forbid();
        }

        var targetUserId = all ? null : userId ?? CurrentUserId;
        if (targetUserId is null && !all)
        {
            return Unauthorized();
        }

        var pdf = await _userActivityReportService.ExportPdfAsync(targetUserId, from, to, cancellationToken);
        var fileName = targetUserId is null ? "user-activity-summary.pdf" : "user-activity-report.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    /// <summary>Case register: one row per case with status, amounts, and dates.</summary>
    [HttpGet("cases.csv")]
    [Authorize(Roles = "CaseReviewer,ApprovingOfficial,ComplianceOfficer,SystemAdministrator")]
    public async Task<IActionResult> CasesRegister(CancellationToken cancellationToken)
    {
        var cases = await _dbContext.Cases.AsNoTracking()
            .Select(c => new
            {
                c.CaseNumber,
                c.CaseType,
                c.Status,
                c.Title,
                c.AmountInvolved,
                c.RecoverableAmount,
                c.RecoveredAmount,
                c.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var csv = BuildCsv(
            new[] { "CaseNumber", "CaseType", "Status", "Title", "AmountInvolved", "RecoverableAmount", "RecoveredAmount", "CreatedAtUtc" },
            cases.Select(c => new[]
            {
                c.CaseNumber, c.CaseType.ToString(), c.Status.ToString(), c.Title,
                c.AmountInvolved?.ToString(CultureInfo.InvariantCulture) ?? "",
                c.RecoverableAmount.ToString(CultureInfo.InvariantCulture),
                c.RecoveredAmount.ToString(CultureInfo.InvariantCulture),
                c.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture)
            }));

        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "cases-register.csv");
    }

    /// <summary>Recovery register: one row per recovery transaction.</summary>
    [HttpGet("recoveries.csv")]
    [Authorize(Roles = "FinanceOfficer,ApprovingOfficial,ComplianceOfficer,SystemAdministrator")]
    public async Task<IActionResult> RecoveriesRegister(CancellationToken cancellationToken)
    {
        var recoveries = await _dbContext.Recoveries.AsNoTracking()
            .Join(_dbContext.Cases.AsNoTracking(), r => r.CaseId, c => c.Id, (r, c) => new { r, c.CaseNumber })
            .ToListAsync(cancellationToken);

        var csv = BuildCsv(
            new[] { "CaseNumber", "Amount", "Status", "Reference", "CreatedAtUtc" },
            recoveries.Select(x => new[]
            {
                x.CaseNumber, x.r.Amount.ToString(CultureInfo.InvariantCulture), x.r.Status.ToString(),
                x.r.Reference ?? "", x.r.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture)
            }));

        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "recoveries-register.csv");
    }

    /// <summary>Contract register: value, utilisation and expiry per contract.</summary>
    [HttpGet("contracts.csv")]
    [Authorize(Roles = "ContractOfficer,ComplianceOfficer,SystemAdministrator")]
    public async Task<IActionResult> ContractsRegister(CancellationToken cancellationToken)
    {
        var contracts = await _dbContext.Contracts.AsNoTracking().ToListAsync(cancellationToken);

        var csv = BuildCsv(
            new[] { "ContractNumber", "Title", "OriginalValue", "CurrentValue", "UtilisedValue", "StartDateUtc", "ExpiryDateUtc" },
            contracts.Select(c => new[]
            {
                c.ContractNumber, c.Title,
                c.OriginalValue.ToString(CultureInfo.InvariantCulture),
                c.CurrentValue.ToString(CultureInfo.InvariantCulture),
                c.UtilisedValue.ToString(CultureInfo.InvariantCulture),
                c.StartDateUtc.ToString("o", CultureInfo.InvariantCulture),
                c.ExpiryDateUtc.ToString("o", CultureInfo.InvariantCulture)
            }));

        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "contracts-register.csv");
    }

    private static string BuildCsv(IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', headers.Select(EscapeCsvField)));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', row.Select(EscapeCsvField)));
        }

        return builder.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
