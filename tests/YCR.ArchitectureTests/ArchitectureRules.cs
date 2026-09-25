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

    /// <summary>
    /// F-002 plan P5 (V6): <c>IAuditWriter.RecordSignIn</c> names an actor that is not the
    /// authenticated principal, so only <c>LoginHandler</c> — which has just verified the password —
    /// may call it. Its compiler-generated async state machine is nested in it and is allowed too.
    /// </summary>
    public static readonly IArchRule OnlyLoginHandlerRecordsSignIn = Types().That()
        .DoNotHaveFullNameMatching("^YCR\\.Application\\.Identity\\.Login\\.LoginHandler(?:[+/].*)?$")
        .Should()
        .NotCallAny(MethodMembers().That().HaveNameStartingWith("RecordSignIn("))
        .WithoutRequiringPositiveResults();

    /// <summary>The same call with no exemption: must fail on the source, proving the call is seen.</summary>
    public static readonly IArchRule NobodyRecordsSignIn = Types()
        .Should()
        .NotCallAny(MethodMembers().That().HaveNameStartingWith("RecordSignIn("))
        .WithoutRequiringPositiveResults();

    /// <summary>F-002 plan P1: ASP.NET Core Identity lives in Infrastructure, never in Application.</summary>
    public static readonly IArchRule ApplicationMustNotDependOnAspNetIdentity = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Application(?:\\..*)?$")
        .Should()
        .NotDependOnAny(Types().That().ResideInNamespaceMatching("^Microsoft\\.AspNetCore\\.Identity(?:\\..*)?$"))
        .WithoutRequiringPositiveResults();

    /// <summary>
    /// ADR-0025 item 4 (REQUIRED CONTROL; F-004 plan P15): a module's contract is built from
    /// primitives and its own records only. It exposes no module domain type, no module context and
    /// nothing from EF Core, because an <c>IQueryable&lt;Station&gt;</c> leaks the domain as surely
    /// as a <c>Station</c>.
    /// </summary>
    public static readonly IArchRule ContractsMustNotDependOnModuleDomainOrContext = Types().That()
        .ResideInNamespaceMatching("^YCR\\.Application\\.[^.]+\\.Contracts(?:\\..*)?$")
        .Should()
        .NotDependOnAny(Types().That()
            .HaveFullNameMatching("^YCR\\.Domain\\.(?!Common(?:\\.|$)).+")
            .Or().HaveFullNameMatching("^YCR\\.Application\\.[^.]+\\.I[^.]*DbContext$")
            .Or().ResideInNamespaceMatching("^Microsoft\\.EntityFrameworkCore(?:\\..*)?$"))
        .WithoutRequiringPositiveResults();

    private static string RegexEscape(string value) => System.Text.RegularExpressions.Regex.Escape(value);
}
