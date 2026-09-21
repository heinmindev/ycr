using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace YCR.ArchitectureTests;

public static class ArchitectureRules
{
    public static readonly string[] Modules =
    [
        "Identity", "Network", "Timetable", "Fare", "Ticketing",
        "Payments", "Operations", "Reporting", "Audit"
    ];

    public static readonly IArchRule CommonMustNotDependOnAnyModule = Types().That()
        .ResideInNamespaceMatching("^YCR\\.(Domain|Application)\\.Common(?:\\..*)?$")
        .Should()
        .NotDependOnAny(
            Types().That().HaveFullNameMatching(
                "^YCR\\.(Domain|Application)\\.(?!Common(?:\\.|$)).+"))
        .WithoutRequiringPositiveResults();

    public static IArchRule ApplicationModuleMayDependOnlyOnAllowedTypes(string module)
    {
        var modulePattern = RegexEscape(module);
        var otherApplicationPattern =
            $"^YCR\\.Application\\.(?!{modulePattern}(?:\\.|$)|Common(?:\\.|$)|(?:{string.Join("|", Modules.Select(RegexEscape))})\\.Contracts(?:\\.|$)).+";
        var otherDomainPattern =
            $"^YCR\\.Domain\\.(?!{modulePattern}(?:\\.|$)|Common(?:\\.|$)).+";

        return Types().That()
            .ResideInNamespaceMatching($"^YCR\\.Application\\.{modulePattern}(?:\\..*)?$")
            .Should()
            .NotDependOnAny(
                Types().That()
                    .HaveFullNameMatching(otherApplicationPattern)
                    .Or().HaveFullNameMatching(otherDomainPattern)
                    .Or().ResideInNamespaceMatching("^YCR\\.Infrastructure(?:\\..*)?$")
                    .Or().ResideInNamespaceMatching("^YCR\\.Api(?:\\..*)?$"))
            .WithoutRequiringPositiveResults();
    }

    public static IArchRule DomainModuleMustNotDependOnOtherDomains(string module)
    {
        var modulePattern = RegexEscape(module);
        var otherDomainPattern =
            $"^YCR\\.Domain\\.(?!{modulePattern}(?:\\.|$)|Common(?:\\.|$)).+";

        return Types().That()
            .ResideInNamespaceMatching($"^YCR\\.Domain\\.{modulePattern}(?:\\..*)?$")
            .Should()
            .NotDependOnAny(Types().That().HaveFullNameMatching(otherDomainPattern))
            .WithoutRequiringPositiveResults();
    }

    public static readonly IArchRule DomainMustNotDependOnApplicationInfrastructureApi = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Domain(?:\\..*)?$")
        .Should()
        .NotDependOnAny(
            Types().That()
                .ResideInNamespaceMatching("^YCR\\.Application(?:\\..*)?$")
                .Or().ResideInNamespaceMatching("^YCR\\.Infrastructure(?:\\..*)?$")
                .Or().ResideInNamespaceMatching("^YCR\\.Api(?:\\..*)?"))
        .WithoutRequiringPositiveResults();

    public static readonly IArchRule DomainMustNotDependOnEntityFrameworkCore = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Domain(?:\\..*)?$")
        .Should()
        .NotDependOnAny(Types().That().ResideInNamespaceMatching("^Microsoft\\.EntityFrameworkCore(?:\\..*)?$"))
        .WithoutRequiringPositiveResults();

    public static readonly IArchRule ApplicationMustNotDependOnInfrastructureOrApi = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Application(?:\\..*)?$")
        .Should()
        .NotDependOnAny(
            Types().That()
                .ResideInNamespaceMatching("^YCR\\.Infrastructure(?:\\..*)?$")
                .Or().ResideInNamespaceMatching("^YCR\\.Api(?:\\..*)?"))
        .WithoutRequiringPositiveResults();

    public static readonly IArchRule ReportingMustUseOnlyReadContext = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Application\\.Reporting(?:\\..*)?$")
        .Should()
        .NotDependOnAny(
            Types().That()
                .HaveFullNameMatching("^YCR\\.Application\\..*\\.I.*DbContext$")
                .And().DoNotHaveFullName("YCR.Application.Reporting.IReportingReadContext")
                .Or().HaveFullNameMatching("^YCR\\.Infrastructure\\.Persistence\\.YcrDbContext$"))
        .WithoutRequiringPositiveResults();

    public static readonly IArchRule ApplicationMustNotDependOnSqlServerProvider = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Application(?:\\..*)?$")
        .Should()
        .NotDependOnAny(
            Types().That()
                .HaveFullNameContaining("Microsoft.EntityFrameworkCore.SqlServer")
                .Or().HaveFullNameContaining("Microsoft.Data.SqlClient"))
        .WithoutRequiringPositiveResults();

    public static readonly IArchRule ApiMustNotDependOnModuleDomain = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Api(?:\\..*)?$")
        .Should()
        .NotDependOnAny(Types().That().ResideInNamespaceMatching("^YCR\\.Domain\\.(?!Common)([^.]+)(?:\\..*)?$"))
        .WithoutRequiringPositiveResults();

    public static readonly IArchRule ApiMayUseOnlyAllowlistedCommonTypes = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Api(?:\\..*)?$")
        .Should()
        .NotDependOnAny(
            Types().That()
                .HaveFullNameMatching(
                    "^YCR\\.Domain\\.Common\\.(?!(Result($|`1$)|Error$|ErrorType$)).+$"))
        .WithoutRequiringPositiveResults();

    private static string RegexEscape(string value) => System.Text.RegularExpressions.Regex.Escape(value);
}
