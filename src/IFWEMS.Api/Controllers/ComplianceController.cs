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

    public ComplianceController(IComplianceRuleEngine ruleEngine, IEmergencyOverrideService overrideService)
    {
        _ruleEngine = ruleEngine;
        _overrideService = overrideService;
    }

    /// <summary>Real-time compliance check returning risk score, rating and recommended action. FR-010.</summary>
    [HttpPost("check")]
    [ProducesResponseType(typeof(TransactionCheckResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<TransactionCheckResult>> Check([FromBody] TransactionCheckRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _ruleEngine.EvaluateAsync(request, cancellationToken));
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
