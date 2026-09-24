using YCR.Domain.Identity;

namespace YCR.Domain.Tests.Identity;

/// <summary>R19 / S30: the eight canonical role identifiers (provisional tech-lead ruling, OQ12).</summary>
public sealed class RoleNamesTests
{
    [Fact]
    public void RoleNames_All_IsExactlyTheEightCanonicalIdentifiers()
    {
        Assert.Equal(
            [
                "SystemAdministrator",
                "RailwayAdministrator",
                "StationManager",
                "TicketOperator",
                "TicketInspector",
                "FinanceOfficer",
                "Auditor",
                "ReportingUser",
            ],
            RoleNames.All);
    }
}
