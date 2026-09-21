using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ReflectionAssembly = System.Reflection.Assembly;
using Xunit;
using ApiUsingAggregateRoot = YCR.Api.Violations.ApiUsingAggregateRoot;
using ApiUsingNetworkDomain = YCR.Api.Violations.ApiUsingNetworkDomain;
using ApplicationUsingInfrastructure = YCR.Application.Network.Violations.ApplicationUsingInfrastructure;
using ApplicationUsingSqlServerProvider = YCR.Application.Violations.ApplicationUsingSqlServerProvider;
using CommonUsingNetworkModule = YCR.Domain.Common.Violations.CommonUsingNetworkModule;
using DomainUsingEntityFrameworkCore = YCR.Domain.Network.Violations.DomainUsingEntityFrameworkCore;
using DomainUsingOtherModule = YCR.Domain.Network.Violations.DomainUsingOtherModule;
using DomainUsingApplication = YCR.Domain.Network.Violations.DomainUsingApplication;
using NetworkUsingTicketingContext = YCR.Application.Network.Violations.NetworkUsingTicketingContext;
using NetworkUsingTicketingDomain = YCR.Application.Network.Violations.NetworkUsingTicketingDomain;
using ReportingUsingNetworkContext = YCR.Application.Reporting.Violations.ReportingUsingNetworkContext;
using SourceUsingAuthenticationHandler = YCR.Api.Violations.SourceUsingAuthenticationHandler;

namespace YCR.ArchitectureTests;

public sealed class ArchitectureRuleTests
{
    private static readonly Architecture SourceArchitecture = new ArchLoader()
        .LoadAssemblies(
            typeof(YCR.Domain.Common.Result).Assembly,
            typeof(YCR.Application.Network.INetworkDbContext).Assembly,
            typeof(YCR.Infrastructure.Persistence.YcrDbContext).Assembly,
            ReflectionAssembly.Load("YCR.Api"))
        .Build();

    private static readonly Architecture ViolationsArchitecture = new ArchLoader()
        .LoadAssemblies(
            typeof(NetworkUsingTicketingContext).Assembly,
            typeof(YCR.Domain.Common.Result).Assembly,
            typeof(YCR.Application.Network.INetworkDbContext).Assembly,
            typeof(YCR.Infrastructure.Persistence.YcrDbContext).Assembly,
            ReflectionAssembly.Load("YCR.Api"),
            typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly,
            typeof(Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsExtensions).Assembly,
            typeof(Microsoft.Data.SqlClient.SqlConnection).Assembly,
            typeof(Microsoft.AspNetCore.Authentication.AuthenticationHandler<>).Assembly)
        .Build();

    [Fact]
    public void ApplicationModuleAllowlist_DetectsForeignContextAndDomainFixtures()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApplicationModuleMayDependOnlyOnAllowedTypes("Network"),
            nameof(NetworkUsingTicketingContext));
        AssertRuleProtectsFixture(
            ArchitectureRules.ApplicationModuleMayDependOnlyOnAllowedTypes("Network"),
            nameof(NetworkUsingTicketingDomain));
    }

    [Fact]
    public void ApplicationAndDomainRules_AreDefinedForEveryBoundedContextModule()
    {
        foreach (var module in ArchitectureRules.Modules)
        {
            Assert.True(
                ArchitectureRules.ApplicationModuleMayDependOnlyOnAllowedTypes(module).HasNoViolations(SourceArchitecture),
                module);
            Assert.True(
                ArchitectureRules.DomainModuleMustNotDependOnOtherDomains(module).HasNoViolations(SourceArchitecture),
                module);
        }
    }

    [Fact]
    public void DomainModuleBoundary_DetectsForeignDomainFixture()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.DomainModuleMustNotDependOnOtherDomains("Network"),
            nameof(DomainUsingOtherModule));
    }

    [Fact]
    public void CommonKernel_WithModuleDependency_DetectsViolation()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.CommonMustNotDependOnAnyModule,
            nameof(CommonUsingNetworkModule));
    }

    [Fact]
    public void Reporting_AllowsOnlyReportingReadContext()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ReportingMustUseOnlyReadContext,
            nameof(ReportingUsingNetworkContext));
    }

    [Fact]
    public void DomainLayerRules_DetectApplicationAndEfDependencies()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.DomainMustNotDependOnApplicationInfrastructureApi,
            nameof(DomainUsingApplication));
        AssertRuleProtectsFixture(
            ArchitectureRules.DomainMustNotDependOnEntityFrameworkCore,
            nameof(DomainUsingEntityFrameworkCore));
    }

    [Fact]
    public void ApplicationLayerRule_DetectsInfrastructureDependency()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApplicationMustNotDependOnInfrastructureOrApi,
            nameof(ApplicationUsingInfrastructure));
    }

    [Fact]
    public void Application_DependentOnSqlServerProvider_IsDetected()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApplicationMustNotDependOnSqlServerProvider,
            nameof(ApplicationUsingSqlServerProvider));
    }

    [Fact]
    public void Api_DependentOnModuleDomainOrNonAllowlistedCommonType_IsDetected()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApiMustNotDependOnModuleDomain,
            nameof(ApiUsingNetworkDomain));
        AssertRuleProtectsFixture(
            ArchitectureRules.ApiMayUseOnlyAllowlistedCommonTypes,
            nameof(ApiUsingAggregateRoot));
    }

    [Fact]
    public void Application_ReferencesNoSqlServerAssemblies()
    {
        var references = typeof(YCR.Application.Network.INetworkDbContext).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Microsoft.EntityFrameworkCore.SqlServer", references);
        Assert.DoesNotContain("Microsoft.Data.SqlClient", references);
    }

    [Fact]
    public void Src_ContainingAuthenticationHandler_IsDetectedAtAnyInheritanceDepth()
    {
        var sourceAssemblies = new[]
        {
            typeof(YCR.Domain.Common.Result).Assembly,
            typeof(YCR.Application.Network.INetworkDbContext).Assembly,
            typeof(YCR.Infrastructure.Persistence.YcrDbContext).Assembly,
            ReflectionAssembly.Load("YCR.Api")
        };

        var sourceViolations = sourceAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type != typeof(Microsoft.AspNetCore.Authentication.IAuthenticationHandler))
            .Where(type => typeof(Microsoft.AspNetCore.Authentication.IAuthenticationHandler).IsAssignableFrom(type))
            .ToArray();

        Assert.Empty(sourceViolations);
        Assert.True(typeof(Microsoft.AspNetCore.Authentication.IAuthenticationHandler)
            .IsAssignableFrom(typeof(SourceUsingAuthenticationHandler)));
    }

    /// <summary>
    /// Hein's ruling, 2026-09-20: every <c>IAuditSnapshot</c> must be a record and must live in
    /// <c>YCR.Application.&lt;Module&gt;</c>, never in <c>YCR.Domain</c>.
    /// </summary>
    /// <remarks>
    /// Written with reflection rather than as an ArchUnit rule because "is a record" is not a
    /// dependency fact — it is detected by the compiler-generated <c>&lt;Clone&gt;$</c> member,
    /// which ArchUnit's fluent API does not express.
    /// </remarks>
    [Fact]
    public void AuditSnapshots_AreRecordsInAnApplicationModuleNamespace()
    {
        var sourceViolations = AuditSnapshotViolationsIn(SourceAssemblies);
        Assert.Empty(sourceViolations);

        // The fixtures prove each half of the rule bites, so the empty result above is trustworthy
        // rather than merely vacuous.
        var planted = AuditSnapshotViolationsIn([typeof(ArchitectureRuleTests).Assembly]);
        Assert.Contains(planted, violation => violation.Contains(nameof(Violations.NonRecordAuditSnapshot), StringComparison.Ordinal));
        Assert.Contains(planted, violation => violation.Contains(nameof(Violations.MisplacedAuditSnapshot), StringComparison.Ordinal));
        Assert.Contains(planted, violation => violation.Contains("DomainAuditSnapshot", StringComparison.Ordinal));
    }

    [Fact]
    public void AuditSnapshots_ExistAtAllSoTheRuleIsNotVacuous()
    {
        var snapshots = SourceAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(YCR.Application.Common.Abstractions.IAuditSnapshot).IsAssignableFrom(type))
            .Where(type => type is { IsInterface: false, IsAbstract: false })
            .ToArray();

        Assert.NotEmpty(snapshots);
    }

    private static readonly ReflectionAssembly[] SourceAssemblies =
    [
        typeof(YCR.Domain.Common.Result).Assembly,
        typeof(YCR.Application.Network.INetworkDbContext).Assembly,
        typeof(YCR.Infrastructure.Persistence.YcrDbContext).Assembly,
        ReflectionAssembly.Load("YCR.Api")
    ];

    private static string[] AuditSnapshotViolationsIn(IEnumerable<ReflectionAssembly> assemblies)
    {
        var moduleNamespaces = ArchitectureRules.Modules
            .Select(module => $"YCR.Application.{module}")
            .ToArray();

        return [.. assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(YCR.Application.Common.Abstractions.IAuditSnapshot).IsAssignableFrom(type))
            .Where(type => type is { IsInterface: false, IsAbstract: false })
            .SelectMany(type =>
            {
                var problems = new List<string>();

                // A record carries a compiler-generated clone method; nothing else does.
                var isRecord = type.GetMethod(
                    "<Clone>$",
                    System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic) is not null;
                if (!isRecord)
                {
                    problems.Add($"{type.FullName} implements IAuditSnapshot but is not a record.");
                }

                var containing = type.Namespace ?? string.Empty;
                var placed = moduleNamespaces.Any(module =>
                    containing == module || containing.StartsWith($"{module}.", StringComparison.Ordinal));
                if (!placed)
                {
                    problems.Add(
                        $"{type.FullName} implements IAuditSnapshot but does not live in YCR.Application.<Module>.");
                }

                return problems;
            })];
    }

    private static void AssertRuleProtectsFixture(IArchRule rule, string fixtureName)
    {
        var sourceResults = rule.Evaluate(SourceArchitecture).Where(result => !result.Passed).ToArray();
        Assert.True(rule.HasNoViolations(SourceArchitecture), string.Join(Environment.NewLine, sourceResults));

        Assert.False(rule.HasNoViolations(ViolationsArchitecture));
        Assert.Contains(
            fixtureName,
            string.Join(Environment.NewLine, rule.Evaluate(ViolationsArchitecture).Where(result => !result.Passed)));
    }
}
