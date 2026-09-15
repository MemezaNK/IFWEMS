using IFWEMS.Domain.Entities;

namespace IFWEMS.Domain.Tests;

public class CaseLifecycleTests
{
    [Fact]
    public void NewInvestigation_DefaultsToInProgress()
    {
        var investigation = new Investigation { CaseId = Guid.NewGuid(), InvestigatorUserId = Guid.NewGuid() };

        Assert.Equal(InvestigationStatus.InProgress, investigation.Status);
    }

    [Fact]
    public void NewRecovery_DefaultsToCaptured()
    {
        var recovery = new Recovery { CaseId = Guid.NewGuid(), Amount = 1000m, CapturedByUserId = Guid.NewGuid() };

        Assert.Equal(RecoveryStatus.Captured, recovery.Status);
    }

    [Fact]
    public void CaseStatusHistory_RecordsFromAndToStatus()
    {
        var caseId = Guid.NewGuid();
        var history = new CaseStatusHistory
        {
            CaseId = caseId,
            FromStatus = CaseStatus.UnderAssessment,
            ToStatus = CaseStatus.UnderInvestigation,
            ChangedBy = "test.user",
            Reason = "Escalated for investigation"
        };

        Assert.Equal(CaseStatus.UnderAssessment, history.FromStatus);
        Assert.Equal(CaseStatus.UnderInvestigation, history.ToStatus);
        Assert.Equal(caseId, history.CaseId);
    }

    [Fact]
    public void CorrectiveAction_DefaultsToPlanned()
    {
        var action = new CorrectiveAction
        {
            CaseId = Guid.NewGuid(),
            ControlId = Guid.NewGuid(),
            Description = "Implement additional PO approval control"
        };

        Assert.Equal(CorrectiveActionStatus.Planned, action.Status);
    }
}
