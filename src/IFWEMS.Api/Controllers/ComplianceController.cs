using System.Security.Claims;
using IFWEMS.Application.Common.Interfaces;
using IFWEMS.Application.Compliance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IFWEMS.Api.Controllers;

[ApiController]
[Route("api/compliance")]
[Authorize]
public class ComplianceController : ControllerBase
{
    private readonly IComplianceRuleEngine _ruleEngine;
    private readonly IEmergencyOverrideService _overrideService;
    private readonly ILogger<ComplianceController> _logger;

    public ComplianceController(IComplianceRuleEngine ruleEngine, IEmergencyOverrideService overrideService, ILogger<ComplianceController> logger)
    {
        _ruleEngine = ruleEngine;
        _overrideService = overrideService;
        _logger = logger;
    }

    /// <summary>Real-time compliance check returning risk score, rating and recommended action. FR-010.</summary>
    [HttpPost("check")]
    [ProducesResponseType(typeof(TransactionCheckResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<TransactionCheckResult>> Check([FromBody] TransactionCheckRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _ruleEngine.EvaluateAsync(request, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail-closed decision (NFR 4.4 #28): if the rules engine itself is unavailable,
            // the transaction must NOT be silently let through. Prioritising compliance risk
            // over availability here is deliberate, since bypassing screening on an outage
            // could allow irregular/unauthorised expenditure to be paid with no record of it
            // ever having been checked. Callers receive a RED/BLOCK result and must retry or
            // escalate via the emergency-override workflow (FR-013), which still records and
            // reviews the transaction.
            _logger.LogError(ex, "Compliance rule engine evaluation failed for transaction {TransactionReference}; failing closed.", request.TransactionReference);
            return Ok(new TransactionCheckResult(100, "RED", new[] { "ENGINE_UNAVAILABLE" }, "BLOCK"));
        }
    }

    /// <summary>
    /// Controlled emergency-override workflow: captures the override transaction and
    /// automatically raises a post-transaction compliance review case. FR-013/FR-014.
    /// </summary>
    [HttpPost("override")]
    [Authorize(Roles = "ApprovingOfficial,SystemAdministrator")]
    public async Task<IActionResult> Override([FromBody] EmergencyOverrideRequest request, CancellationToken cancellationToken)
    {
        var transactionId = await _overrideService.CaptureOverrideAsync(request, User.FindFirstValue(ClaimTypes.Name), cancellationToken);
        return Ok(new { transactionId });
    }
}
