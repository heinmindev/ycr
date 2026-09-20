using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Network.CreateStation;
using YCR.Application.Network.DeactivateStation;
using YCR.Domain.Common;

namespace YCR.Application.Tests.Network;

/// <summary>Spec S2, S7, S8 and S27 against real SQL Server under <c>ycr_app</c>.</summary>
public sealed class DeactivateStationHandlerTests(SqlServerFixture fixture) : NetworkHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "deactivate_station";

    [Fact]
    public async Task Handle_WhenActive_DeactivatesAndWritesOneAuditEvent()
    {
        await using var provider = BuildProvider();
        var stationId = await CreateAsync(provider, "INS");

        await using (var scope = provider.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<DeactivateStationHandler>()
                .Handle(new DeactivateStationCommand(stationId), CancellationToken);
            Assert.True(result.IsSuccess);
        }

        Assert.Equal(0, await ScalarAsync<int>(
            "SELECT CAST([IsActive] AS int) FROM [network].[Stations];"));
        Assert.Equal(1, await ScalarAsync<int>(
            "SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] = N'Network.StationDeactivated';"));

        // before/after are the snapshot records, and they differ in exactly the field that changed.
        Assert.Equal(
            """{"code":"INS","nameEn":"Insein","nameMy":"အင်းစိန်","isActive":true}""",
            await ScalarAsync<string>(
                "SELECT [BeforeJson] FROM [audit].[AuditEvents] WHERE [Action] = N'Network.StationDeactivated';"));
        Assert.Equal(
            """{"code":"INS","nameEn":"Insein","nameMy":"အင်းစိန်","isActive":false}""",
            await ScalarAsync<string>(
                "SELECT [AfterJson] FROM [audit].[AuditEvents] WHERE [Action] = N'Network.StationDeactivated';"));
    }

    [Fact]
    public async Task Handle_WhenStationInactive_ReturnsBusinessRuleError()
    {
        await using var provider = BuildProvider();
        var stationId = await CreateAsync(provider, "BGO");
        await DeactivateAsync(provider, stationId);

        var second = await DeactivateAsync(provider, stationId);

        Assert.True(second.IsFailure);
        Assert.Equal("Network.StationAlreadyInactive", second.Error.Code);
        // ADR-0004 maps BusinessRule to 422, which is what spec S7 asks the API to return.
        Assert.Equal(ErrorType.BusinessRule, second.Error.Type);

        Assert.Equal(1, await ScalarAsync<int>(
            "SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] = N'Network.StationDeactivated';"));
    }

    [Fact]
    public async Task Handle_WithUnknownId_ReturnsNotFound()
    {
        await using var provider = BuildProvider();

        var result = await DeactivateAsync(provider, Guid.CreateVersion7());

        Assert.True(result.IsFailure);
        Assert.Equal("Network.StationNotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents];"));
    }

    /// <summary>
    /// S27: two concurrent deactivations of the same active station. <c>IsActive</c> is an EF
    /// concurrency token, so the loser's <c>UPDATE</c> matches zero rows and its audit row rolls
    /// back with it — one success, one 422, and exactly one event.
    /// </summary>
    /// <remarks>
    /// Separate providers give each request its own connection and change tracker; sharing one
    /// would have both requests reading the same tracked entity and prove nothing.
    /// </remarks>
    [Fact]
    public async Task Handle_WithParallelDeactivations_ReturnsOneSuccessOneConflictAndWritesOneAuditEvent()
    {
        await using var setup = BuildProvider();
        var stationId = await CreateAsync(setup, "SHW");

        await using var first = BuildProvider();
        await using var second = BuildProvider();

        var results = await Task.WhenAll(
            DeactivateAsync(first, stationId),
            DeactivateAsync(second, stationId));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        var failure = Assert.Single(results, result => result.IsFailure);
        Assert.Equal("Network.StationAlreadyInactive", failure.Error.Code);

        Assert.Equal(1, await ScalarAsync<int>(
            "SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] = N'Network.StationDeactivated';"));
        Assert.Equal(0, await ScalarAsync<int>("SELECT CAST([IsActive] AS int) FROM [network].[Stations];"));
    }

    private async Task<Guid> CreateAsync(IServiceProvider provider, string code)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<CreateStationHandler>()
            .Handle(new CreateStationCommand(code, NameFor(code), MyanmarNameFor(code)), CancellationToken);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private async Task<Result> DeactivateAsync(IServiceProvider provider, Guid stationId)
    {
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<DeactivateStationHandler>()
            .Handle(new DeactivateStationCommand(stationId), CancellationToken);
    }

    private static string NameFor(string code) => code switch
    {
        "INS" => "Insein",
        "BGO" => "Bago",
        _ => "Shwedagon"
    };

    private static string MyanmarNameFor(string code) => code switch
    {
        "INS" => "အင်းစိန်",
        "BGO" => "ပဲခူး",
        _ => "ရွှေတိဂုံ"
    };
}
