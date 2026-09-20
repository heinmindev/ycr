using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ReflectionAssembly = System.Reflection.Assembly;
using Xunit;
using ApiUsingAggregateRoot = YCR.Api.Violations.ApiUsingAggregateRoot;
using ApiUsingNetworkDomain = YCR.Api.Violations.ApiUsingNetworkDomain;
using ApplicationUsingInfrastructure = YCR.Application.Network.Violations.ApplicationUsingInfrastructure;
using ApplicationUsingSqlServerProvider = YCR.Application.Violations.ApplicationUsingSqlServerProvider;
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
