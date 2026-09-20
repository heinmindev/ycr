using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ReflectionAssembly = System.Reflection.Assembly;
using ArchUnitNET.Loader;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using NetworkUsingTicketingContext = YCR.Application.Network.Violations.NetworkUsingTicketingContext;
using NetworkUsingTicketingDomain = YCR.Application.Network.Violations.NetworkUsingTicketingDomain;
using ReportingUsingWriteContext = YCR.Application.Reporting.Violations.ReportingUsingWriteContext;
using ApiUsingNetworkDomain = YCR.Api.Violations.ApiUsingNetworkDomain;
using ApiUsingAggregateRoot = YCR.Api.Violations.ApiUsingAggregateRoot;
using ApplicationUsingSqlServerProvider = YCR.Application.Violations.ApplicationUsingSqlServerProvider;
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
            typeof(Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsExtensions).Assembly,
            typeof(Microsoft.Data.SqlClient.SqlConnection).Assembly,
            typeof(Microsoft.AspNetCore.Authentication.AuthenticationHandler<>).Assembly)
        .Build();

    [Fact]
    public void NetworkApplication_DependingOnTicketingContext_IsDetected()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.NetworkApplicationMustNotDependOnTicketingContext,
            nameof(NetworkUsingTicketingContext));
    }

    [Fact]
    public void NetworkApplication_DependingOnTicketingDomain_IsDetected()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.NetworkApplicationMustNotDependOnTicketingDomain,
            nameof(NetworkUsingTicketingDomain));
    }

    [Fact]
    public void Reporting_DependingOnWriteContext_IsDetected()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ReportingMustNotDependOnWriteContext,
            nameof(ReportingUsingWriteContext));
    }

    [Fact]
    public void Api_DependingOnModuleDomainNamespace_IsDetected()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApiMustNotDependOnModuleDomain,
            nameof(ApiUsingNetworkDomain));
    }

    [Fact]
    public void Api_DependingOnNonAllowlistedCommonType_IsDetected()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApiMayUseOnlyAllowlistedCommonTypes,
            nameof(ApiUsingAggregateRoot));
    }

    [Fact]
    public void Application_DependentOnSqlServerProvider_IsDetected()
    {
        AssertRuleProtectsFixture(
            ArchitectureRules.ApplicationMustNotDependOnSqlServerProvider,
            nameof(ApplicationUsingSqlServerProvider));
    }

    [Fact]
    public void Src_ContainingAuthenticationHandler_IsDetected()
    {
        var authenticationHandler = typeof(Microsoft.AspNetCore.Authentication.AuthenticationHandler<>);
        var sourceAssemblies = new[]
        {
            typeof(YCR.Domain.Common.Result).Assembly,
            typeof(YCR.Application.Network.INetworkDbContext).Assembly,
            typeof(YCR.Infrastructure.Persistence.YcrDbContext).Assembly,
            ReflectionAssembly.Load("YCR.Api")
        };

        var sourceViolations = sourceAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.BaseType?.IsGenericType == true)
            .Where(type => type.BaseType!.GetGenericTypeDefinition() == authenticationHandler)
            .ToArray();

        Assert.Empty(sourceViolations);

        var fixtureType = typeof(SourceUsingAuthenticationHandler);
        Assert.Equal(authenticationHandler, fixtureType.BaseType!.GetGenericTypeDefinition());
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
