using IFWEMS.Domain.Entities;

namespace IFWEMS.Domain.Tests;

public class CaseTests
{
    [Fact]
    public void NewCase_DefaultsToDraftStatus()
    {
        var entity = new Case
        {
            CaseNumber = "DOH-IE-2026-000001",
            CaseType = CaseType.IrregularExpenditure,
            OrgUnit = new OrgUnit { Code = "DIST-01-FAC-01", Name = "Test Facility", Level = "Facility" },
            Title = "Test case"
        };

        Assert.Equal(CaseStatus.Draft, entity.Status);
        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void OrgUnit_ChildAddedToParent_IsReflectedInChildrenCollection()
    {
        var parent = new OrgUnit { Code = "DOH", Name = "Department of Health", Level = "Department" };
        var child = new OrgUnit { Code = "DIST-01", Name = "Metro District", Level = "District", ParentOrgUnit = parent };
        parent.Children.Add(child);

        Assert.Single(parent.Children);
        Assert.Equal(parent, child.ParentOrgUnit);
    }
}
