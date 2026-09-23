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
using RecordSignInOutsideLoginHandler = YCR.Application.Identity.Violations.RecordSignInOutsideLoginHandler;
using IdentityUsingNetworkContext = YCR.Application.Identity.Violations.IdentityUsingNetworkContext;
using NetworkUsingIdentityContext = YCR.Application.Network.Violations.NetworkUsingIdentityContext;
using ApplicationUsingAspNetIdentity = YCR.Application.Violations.ApplicationUsingAspNetIdentity;

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
            typeof(Microsoft.AspNetCore.Authentication.AuthenticationHandler<>).Assembly,
            typeof(Microsoft.AspNetCore.Identity.PasswordHasher<>).Assembly)
        .Build();

    [Fact]
    public void ApplicationModuleAllowlist_WithForeignFixtures_DetectsViolations()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApplicationModuleMayDependOnlyOnAllowedTypes("Network"),
            nameof(NetworkUsingTicketingContext));
        AssertRuleProtectsFixture(
            ArchitectureRules.ApplicationModuleMayDependOnlyOnAllowedTypes("Network"),
            nameof(NetworkUsingTicketingDomain));
    }

    [Fact]
    public void ApplicationAndDomainRules_ForEveryBoundedContext_HaveNoSourceViolations()
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

    /// <summary>
    /// F-002 plan P5, V6: only <c>LoginHandler</c> calls <c>RecordSignIn</c>. The unexempted rule
    /// must fail on the source — proving ArchUnitNET sees the call inside the async handler — and
    /// the exempted rule must pass on the source and fail on the planted fixture.
    /// </summary>
    [Fact]
    public void RecordSignIn_CalledOutsideLoginHandler_IsDetected()
    {
        var seenInSource = string.Join(
            Environment.NewLine,
            ArchitectureRules.NobodyRecordsSignIn.Evaluate(SourceArchitecture).Where(result => !result.Passed));
        Assert.Contains("LoginHandler", seenInSource, StringComparison.Ordinal);

        AssertRuleProtectsFixture(
            ArchitectureRules.OnlyLoginHandlerRecordsSignIn,
            nameof(RecordSignInOutsideLoginHandler));
    }

    [Fact]
    public void IdentityApplication_DependingOnAnotherModuleContext_IsDetected()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApplicationModuleMayDependOnlyOnAllowedTypes("Identity"),
            nameof(IdentityUsingNetworkContext));
    }

    [Fact]
    public void NetworkApplication_DependingOnIdentityContext_IsDetected()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApplicationModuleMayDependOnlyOnAllowedTypes("Network"),
            nameof(NetworkUsingIdentityContext));
    }

    [Fact]
    public void Application_DependingOnAspNetIdentity_IsDetected()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApplicationMustNotDependOnAspNetIdentity,
            nameof(ApplicationUsingAspNetIdentity));
    }

    [Fact]
    public void DomainModuleBoundary_WithForeignDomainFixture_DetectsViolation()
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
    public void ReportingRule_WithNetworkContext_DetectsViolation()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ReportingMustUseOnlyReadContext,
            nameof(ReportingUsingNetworkContext));
    }

    [Fact]
    public void DomainLayerRules_WithForbiddenDependencies_DetectViolations()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.DomainMustNotDependOnApplicationInfrastructureApi,
            nameof(DomainUsingApplication));
        AssertRuleProtectsFixture(
            ArchitectureRules.DomainMustNotDependOnEntityFrameworkCore,
            nameof(DomainUsingEntityFrameworkCore));
    }

    [Fact]
    public void ApplicationLayerRule_WithInfrastructureDependency_DetectsViolation()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApplicationMustNotDependOnInfrastructureOrApi,
            nameof(ApplicationUsingInfrastructure));
    }

    [Fact]
    public void ApplicationRule_WithSqlServerProvider_DetectsViolation()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApplicationMustNotDependOnSqlServerProvider,
            nameof(ApplicationUsingSqlServerProvider));
    }

    [Fact]
    public void ApiRules_WithForbiddenTypes_DetectViolations()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApiMustNotDependOnModuleDomain,
            nameof(ApiUsingNetworkDomain));
        AssertRuleProtectsFixture(
            ArchitectureRules.ApiMayUseOnlyAllowlistedCommonTypes,
            nameof(ApiUsingAggregateRoot));
    }

    [Fact]
    public void ApplicationReferences_WithSqlServerAssemblies_HaveNoReferences()
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
    public void SourceTypes_WithAuthenticationHandler_HaveNoViolations()
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
    public void AuditSnapshots_WithSourceAssemblies_AreValid()
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
    public void AuditSnapshots_WithSourceAssemblies_AreNonEmpty()
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
