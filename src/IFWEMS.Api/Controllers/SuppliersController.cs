using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record SupplierDto(Guid Id, string SupplierCode, string Name, string? RegistrationNumber);
public record CreateSupplierRequest(string SupplierCode, string Name, string? RegistrationNumber);

public record SupplierRiskProfileDto(
    Guid SupplierId, string SupplierCode, string Name,
    int DeviationCount, int CaseCount, int ContractCount,
    decimal TotalContractValue, decimal ConcentrationPercentage, string RiskRating);

/// <summary>
/// Supplier CRUD and aggregated risk profile. FR-031.
/// </summary>
[ApiController]
[Route("api/suppliers")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public SuppliersController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SupplierDto>>> GetAll(CancellationToken cancellationToken)
    {
        var suppliers = await _dbContext.Suppliers.AsNoTracking()
            .Select(s => new SupplierDto(s.Id, s.SupplierCode, s.Name, s.RegistrationNumber))
            .ToListAsync(cancellationToken);

        return Ok(suppliers);
    }

    [HttpPost]
    [Authorize(Roles = "ContractOfficer,SystemAdministrator")]
    public async Task<ActionResult<SupplierDto>> Create([FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var entity = new Supplier { SupplierCode = request.SupplierCode, Name = request.Name, RegistrationNumber = request.RegistrationNumber };
        _dbContext.Suppliers.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new SupplierDto(entity.Id, entity.SupplierCode, entity.Name, entity.RegistrationNumber);
        return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, dto);
    }

    /// <summary>
    /// Aggregated risk profile: deviation/case counts, contract concentration. FR-031.
    /// Concentration is this supplier's total contract value as a percentage of all
    /// suppliers' total contract value (a simple spend-concentration indicator).
    /// </summary>
    [HttpGet("{id:guid}/risk-profile")]
    public async Task<ActionResult<SupplierRiskProfileDto>> GetRiskProfile(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier is null) return NotFound();

        var contractCount = await _dbContext.Contracts.CountAsync(c => c.SupplierId == id, cancellationToken);
        var supplierContractValue = await _dbContext.Contracts.Where(c => c.SupplierId == id).SumAsync(c => (decimal?)c.CurrentValue, cancellationToken) ?? 0m;
        var totalContractValue = await _dbContext.Contracts.SumAsync(c => (decimal?)c.CurrentValue, cancellationToken) ?? 0m;
        var caseCount = await _dbContext.Cases.CountAsync(c => c.SupplierId == id, cancellationToken);

        var concentration = totalContractValue == 0 ? 0 : (supplierContractValue / totalContractValue) * 100m;

        var riskRating = (supplier.DeviationCount, caseCount, concentration) switch
        {
            _ when supplier.DeviationCount >= 5 || caseCount >= 3 || concentration >= 30 => "HIGH",
            _ when supplier.DeviationCount >= 2 || caseCount >= 1 || concentration >= 15 => "MEDIUM",
            _ => "LOW"
        };

        return Ok(new SupplierRiskProfileDto(
            supplier.Id, supplier.SupplierCode, supplier.Name,
            supplier.DeviationCount, caseCount, contractCount,
            supplierContractValue, Math.Round(concentration, 2), riskRating));
    }
}
