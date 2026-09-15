using System.Globalization;
using System.Text;
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

    public ReportsController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
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
