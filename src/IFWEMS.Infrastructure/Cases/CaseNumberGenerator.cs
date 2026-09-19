using IFWEMS.Application.Common.Interfaces;
using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Infrastructure.Cases;

/// <summary>
/// Generates case numbers in the format [DEPT]-[TYPE]-[FY]-[SEQUENCE], guaranteeing
/// uniqueness and monotonic sequencing under concurrent/high-availability access via a
/// row-locked atomic increment. FR-020.
/// </summary>
public class CaseNumberGenerator : ICaseNumberGenerator
{
    private const string DepartmentCode = "DOH";

    private static readonly Dictionary<CaseType, string> TypeCodes = new()
    {
        [CaseType.IrregularExpenditure] = "IE",
        [CaseType.FruitlessWasteful] = "FWE",
        [CaseType.UnauthorisedExpenditure] = "UE",
        [CaseType.PotentialNonCompliance] = "PNC"
    };

    private readonly IfwemsDbContext _dbContext;

    public CaseNumberGenerator(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateAsync(CaseType caseType, CancellationToken cancellationToken = default)
    {
        var typeCode = TypeCodes[caseType];
        var fiscalYear = GetCurrentFiscalYearStartYear(DateTime.UtcNow);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        // Atomic, row-locked increment. Creates the sequence row on first use for the (dept, type, FY) tuple.
        var nextValues = await _dbContext.Database.SqlQueryRaw<int>(
            @"MERGE INTO CaseNumberSequences WITH (HOLDLOCK) AS target
              USING (SELECT {0} AS DepartmentCode, {1} AS CaseTypeCode, {2} AS FiscalYear) AS source
                ON target.DepartmentCode = source.DepartmentCode
               AND target.CaseTypeCode = source.CaseTypeCode
               AND target.FiscalYear = source.FiscalYear
              WHEN MATCHED THEN
                UPDATE SET LastValue = target.LastValue + 1
              WHEN NOT MATCHED THEN
                INSERT (Id, DepartmentCode, CaseTypeCode, FiscalYear, LastValue)
                VALUES (NEWID(), source.DepartmentCode, source.CaseTypeCode, source.FiscalYear, 1)
              OUTPUT INSERTED.LastValue AS Value;",
            DepartmentCode, typeCode, fiscalYear)
            .ToListAsync(cancellationToken);
        var nextValue = nextValues.Single();


        await transaction.CommitAsync(cancellationToken);

        return $"{DepartmentCode}-{typeCode}-{fiscalYear}-{nextValue:D6}";
    }

    /// <summary>South African government fiscal year runs April to March; returns the start year.</summary>
    private static int GetCurrentFiscalYearStartYear(DateTime utcNow) =>
        utcNow.Month >= 4 ? utcNow.Year : utcNow.Year - 1;
}
