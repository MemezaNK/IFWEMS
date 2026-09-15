using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record ContractDto(
    Guid Id, string ContractNumber, string Title, Guid SupplierId, decimal OriginalValue,
    decimal CurrentValue, decimal UtilisedValue, decimal UtilisationPercentage,
    DateTime StartDateUtc, DateTime ExpiryDateUtc, int DaysToExpiry, string? AlertLevel);

public record CreateContractRequest(string ContractNumber, string Title, Guid SupplierId, decimal OriginalValue, DateTime StartDateUtc, DateTime ExpiryDateUtc);
public record RecordVariationRequest(decimal NewValue, string Reason);
public record RecordUtilisationRequest(decimal Amount);

/// <summary>
/// Contract value, variation, utilisation and expiry tracking. FR-030.
/// </summary>
[ApiController]
[Route("api/contracts")]
[Authorize]
public class ContractsController : ControllerBase
{
    // Configurable expiry alert thresholds, in days. FR-030.
    private static readonly int[] ExpiryAlertThresholds = { 120, 90, 60, 30, 14 };
    private const decimal UtilisationAlertThreshold = 0.70m;

    private readonly IfwemsDbContext _dbContext;

    public ContractsController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContractDto>>> GetAll(CancellationToken cancellationToken)
    {
        var contracts = await _dbContext.Contracts.AsNoTracking().ToListAsync(cancellationToken);
        return Ok(contracts.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContractDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var contract = await _dbContext.Contracts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        return contract is null ? NotFound() : Ok(ToDto(contract));
    }

    /// <summary>Contracts currently within an expiry or utilisation alert threshold. FR-030.</summary>
    [HttpGet("alerts")]
    public async Task<ActionResult<IEnumerable<ContractDto>>> GetAlerts(CancellationToken cancellationToken)
    {
        var contracts = await _dbContext.Contracts.AsNoTracking().ToListAsync(cancellationToken);
        var withAlerts = contracts.Select(ToDto).Where(c => c.AlertLevel is not null);
        return Ok(withAlerts);
    }

    [HttpPost]
    [Authorize(Roles = "ContractOfficer,SystemAdministrator")]
    public async Task<ActionResult<ContractDto>> Create([FromBody] CreateContractRequest request, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken))
        {
            return BadRequest(new { message = "The referenced supplier does not exist." });
        }

        var entity = new Contract
        {
            ContractNumber = request.ContractNumber,
            Title = request.Title,
            SupplierId = request.SupplierId,
            OriginalValue = request.OriginalValue,
            CurrentValue = request.OriginalValue,
            UtilisedValue = 0,
            StartDateUtc = request.StartDateUtc,
            ExpiryDateUtc = request.ExpiryDateUtc
        };

        _dbContext.Contracts.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
    }

    /// <summary>Record a contract value variation (amendment). FR-030.</summary>
    [HttpPost("{id:guid}/variations")]
    [Authorize(Roles = "ContractOfficer,SystemAdministrator")]
    public async Task<IActionResult> RecordVariation(Guid id, [FromBody] RecordVariationRequest request, CancellationToken cancellationToken)
    {
        var contract = await _dbContext.Contracts.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract is null) return NotFound();

        contract.CurrentValue = request.NewValue;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Record utilisation against the contract's current value. FR-030.</summary>
    [HttpPost("{id:guid}/utilisation")]
    [Authorize(Roles = "ContractOfficer,FinanceOfficer,SystemAdministrator")]
    public async Task<IActionResult> RecordUtilisation(Guid id, [FromBody] RecordUtilisationRequest request, CancellationToken cancellationToken)
    {
        var contract = await _dbContext.Contracts.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract is null) return NotFound();

        contract.UtilisedValue += request.Amount;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static ContractDto ToDto(Contract contract)
    {
        var daysToExpiry = (contract.ExpiryDateUtc.Date - DateTime.UtcNow.Date).Days;
        var utilisationPct = contract.CurrentValue == 0 ? 0 : contract.UtilisedValue / contract.CurrentValue;

        string? alertLevel = null;
        if (daysToExpiry <= ExpiryAlertThresholds.Min())
        {
            alertLevel = "EXPIRY_CRITICAL";
        }
        else if (ExpiryAlertThresholds.Any(t => daysToExpiry <= t))
        {
            alertLevel = "EXPIRY_APPROACHING";
        }
        else if (utilisationPct >= UtilisationAlertThreshold)
        {
            alertLevel = "UTILISATION_HIGH";
        }

        return new ContractDto(
            contract.Id, contract.ContractNumber, contract.Title, contract.SupplierId,
            contract.OriginalValue, contract.CurrentValue, contract.UtilisedValue, utilisationPct,
            contract.StartDateUtc, contract.ExpiryDateUtc, daysToExpiry, alertLevel);
    }
}
