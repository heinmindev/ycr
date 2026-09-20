using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using Microsoft.AspNetCore.Authentication;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace YCR.ArchitectureTests;

public static class ArchitectureRules
{
    public static readonly IArchRule NetworkApplicationMustNotDependOnTicketingContext = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Application\\.Network(\\..*)?$")
        .Should()
        .NotDependOnAny(Types().That().HaveFullNameContaining("YCR.Application.Ticketing.ITicketingDbContext"))
        .WithoutRequiringPositiveResults();

    public static readonly IArchRule NetworkApplicationMustNotDependOnTicketingDomain = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Application\\.Network(\\..*)?$")
        .Should()
        .NotDependOnAny(Types().That().ResideInNamespaceMatching("^YCR\\.Domain\\.Ticketing(\\..*)?$"))
        .WithoutRequiringPositiveResults();

    public static readonly IArchRule ReportingMustNotDependOnWriteContext = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Application\\.Reporting(\\..*)?$")
        .Should()
        .NotDependOnAny(Types().That().HaveFullNameContaining("IReportingWriteContext"))
        .WithoutRequiringPositiveResults();

    public static readonly IArchRule ApiMustNotDependOnModuleDomain = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Api(\\..*)?$")
        .Should()
        .NotDependOnAny(Types().That().ResideInNamespaceMatching("^YCR\\.Domain\\.(?!Common)([^.]+)(\\..*)?$"))
        .WithoutRequiringPositiveResults();

    public static readonly IArchRule ApiMayUseOnlyAllowlistedCommonTypes = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Api(\\..*)?$")
        .Should()
        .NotDependOnAny(
            Types().That()
                .HaveFullNameMatching(
                    "^YCR\\.Domain\\.Common\\.(?!(Result($|`1$)|Error$|ErrorType$)).+$"))
        .WithoutRequiringPositiveResults();

    public static readonly IArchRule ApplicationMustNotDependOnSqlServerProvider = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Application(\\..*)?$")
        .Should()
        .NotDependOnAny(
            Types().That()
                .HaveFullNameContaining("Microsoft.EntityFrameworkCore.SqlServer")
                .Or().HaveFullNameContaining("Microsoft.Data.SqlClient"))
        .WithoutRequiringPositiveResults();

    public static readonly IArchRule SourceMustNotContainAuthenticationHandler = Classes().That()
        .AreAssignableTo(typeof(AuthenticationHandler<>))
        .And().ResideInNamespaceMatching("^YCR\\.")
        .Should()
        .NotBeAssignableTo(typeof(AuthenticationHandler<>))
        .WithoutRequiringPositiveResults();
}
