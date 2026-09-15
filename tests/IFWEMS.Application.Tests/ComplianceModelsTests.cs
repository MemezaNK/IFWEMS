using IFWEMS.Application.Compliance;

namespace IFWEMS.Application.Tests;

public class ComplianceModelsTests
{
    [Fact]
    public void TransactionCheckResult_CarriesFailedRuleCodesAndAction()
    {
        var result = new TransactionCheckResult(75, "RED", new List<string> { "CEILING_EXCEEDED", "EXPIRED_CONTRACT" }, "BLOCK");

        Assert.Equal("RED", result.RiskRating);
        Assert.Equal("BLOCK", result.RecommendedAction);
        Assert.Equal(2, result.FailedRuleCodes.Count);
    }

    [Fact]
    public void EmergencyOverrideRequest_RequiresEvidenceDocument()
    {
        var request = new EmergencyOverrideRequest(
            "TXN-001", Guid.NewGuid(), Guid.NewGuid(), 15000m, "PatientCareEmergency",
            "Urgent medication shortage", Guid.NewGuid(), "MED-001");

        Assert.NotEqual(Guid.Empty, request.EvidenceDocumentId);
        Assert.Equal("PatientCareEmergency", request.EmergencyCategory);
    }
}
