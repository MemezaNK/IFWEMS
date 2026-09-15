using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record BatchImportError(int RowNumber, string Message);
public record BatchImportResultDto(int RowsReceived, int RowsAccepted, int RowsRejected, IReadOnlyList<BatchImportError> Errors);

/// <summary>
/// Validated batch CSV import with reconciliation (rows received/accepted/rejected), used
/// where a direct secured API integration with an external departmental system (BAS, LOGIS,
/// PERSAL, CSD) is not available. FR-050.
/// </summary>
[ApiController]
[Route("api/imports")]
[Authorize(Roles = "SystemAdministrator,ComplianceOfficer")]
public class BatchImportController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public BatchImportController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Import suppliers from CSV: header row SupplierCode,Name,RegistrationNumber.</summary>
    [HttpPost("suppliers")]
    public async Task<ActionResult<BatchImportResultDto>> ImportSuppliers(IFormFile file, CancellationToken cancellationToken)
    {
        var rows = await ReadCsvRowsAsync(file, cancellationToken);
        var errors = new List<BatchImportError>();
        var accepted = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = i + 2; // +1 for 1-based, +1 for header row
            var row = rows[i];

            if (row.Length < 2 || string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1]))
            {
                errors.Add(new BatchImportError(rowNumber, "SupplierCode and Name are required."));
                continue;
            }

            var supplierCode = row[0].Trim();
            if (await _dbContext.Suppliers.AnyAsync(s => s.SupplierCode == supplierCode, cancellationToken))
            {
                errors.Add(new BatchImportError(rowNumber, $"Supplier code '{supplierCode}' already exists."));
                continue;
            }

            _dbContext.Suppliers.Add(new Supplier
            {
                SupplierCode = supplierCode,
                Name = row[1].Trim(),
                RegistrationNumber = row.Length > 2 ? row[2].Trim() : null
            });
            accepted++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new BatchImportResultDto(rows.Count, accepted, rows.Count - accepted, errors));
    }

    /// <summary>Import contracts from CSV: header row ContractNumber,Title,SupplierCode,OriginalValue,StartDateUtc,ExpiryDateUtc.</summary>
    [HttpPost("contracts")]
    public async Task<ActionResult<BatchImportResultDto>> ImportContracts(IFormFile file, CancellationToken cancellationToken)
    {
        var rows = await ReadCsvRowsAsync(file, cancellationToken);
        var errors = new List<BatchImportError>();
        var accepted = 0;
        var suppliersByCode = await _dbContext.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.SupplierCode, cancellationToken);

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = i + 2;
            var row = rows[i];

            if (row.Length < 6)
            {
                errors.Add(new BatchImportError(rowNumber, "Expected 6 columns: ContractNumber,Title,SupplierCode,OriginalValue,StartDateUtc,ExpiryDateUtc."));
                continue;
            }

            if (!suppliersByCode.TryGetValue(row[2].Trim(), out var supplier))
            {
                errors.Add(new BatchImportError(rowNumber, $"Unknown supplier code '{row[2]}'."));
                continue;
            }

            if (!decimal.TryParse(row[3], out var originalValue) ||
                !DateTime.TryParse(row[4], out var startDate) ||
                !DateTime.TryParse(row[5], out var expiryDate))
            {
                errors.Add(new BatchImportError(rowNumber, "OriginalValue/StartDateUtc/ExpiryDateUtc could not be parsed."));
                continue;
            }

            _dbContext.Contracts.Add(new Contract
            {
                ContractNumber = row[0].Trim(),
                Title = row[1].Trim(),
                SupplierId = supplier.Id,
                OriginalValue = originalValue,
                CurrentValue = originalValue,
                UtilisedValue = 0,
                StartDateUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
                ExpiryDateUtc = DateTime.SpecifyKind(expiryDate, DateTimeKind.Utc)
            });
            accepted++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new BatchImportResultDto(rows.Count, accepted, rows.Count - accepted, errors));
    }

    private static async Task<List<string[]>> ReadCsvRowsAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        var lines = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
        }

        // Skip header row.
        return lines.Skip(1).Select(l => l.Split(',')).ToList();
    }
}
