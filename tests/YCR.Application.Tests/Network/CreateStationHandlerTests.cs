using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Authorization;
using YCR.Application.Network;
using YCR.Application.Network.CreateStation;
using YCR.Application.Network.DeactivateStation;
using YCR.Domain.Common;
using YCR.Infrastructure.Persistence;

namespace YCR.Application.Tests.Network;

/// <summary>Spec S1, S5, S6, S13, S14 and S20 against real SQL Server under <c>ycr_app</c>.</summary>
public sealed class CreateStationHandlerTests(SqlServerFixture fixture) : NetworkHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "create_station";

    [Fact]
    public async Task Handle_WithValidCommand_PersistsStationAndWritesAuditEvent()
    {
        var actor = Guid.CreateVersion7();
        await using var provider = BuildProvider(new TestCurrentUser
        {
            UserId = actor,
            Roles = ["StationManager"],
            ClientIp = "198.51.100.4",
            CorrelationId = "corr-create",
            AuthorizedByPermission = Permissions.StationsManage
        });

        Result<Guid> result;
        await using (var scope = provider.CreateAsyncScope())
        {
            result = await scope.ServiceProvider
                .GetRequiredService<CreateStationHandler>()
                .Handle(new CreateStationCommand("INS", "Insein", "အင်းစိန်"), CancellationToken);
        }

        Assert.True(result.IsSuccess);

        await using var readScope = provider.CreateAsyncScope();
        var station = await readScope.ServiceProvider.GetRequiredService<YcrDbContext>()
            .Stations.AsNoTracking().SingleAsync(candidate => candidate.Id == result.Value, CancellationToken);

        Assert.Equal("INS", station.Code.Value);
        Assert.True(station.IsActive);

        Assert.Equal(1, await ScalarAsync<int>(
            "SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] = N'Network.StationCreated';"));
        Assert.Equal(actor, await ScalarAsync<Guid>("SELECT [ActorUserId] FROM [audit].[AuditEvents];"));
        Assert.Equal(
            Permissions.StationsManage,
            await ScalarAsync<string>("SELECT [AuthorizedByPermission] FROM [audit].[AuditEvents];"));
        Assert.Equal(
            NetworkAuditSubjects.Station,
            await ScalarAsync<string>("SELECT [SubjectType] FROM [audit].[AuditEvents];"));
    }

    [Fact]
    public async Task Handle_WithMyanmarName_RoundTripsExactly()
    {
        const string MyanmarName = "ရန်ကုန်ဘူတာကြီး";
        await using var provider = BuildProvider();

        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<CreateStationHandler>()
            .Handle(new CreateStationCommand("YGN", "Yangon Central", MyanmarName), CancellationToken);

        Assert.True(result.IsSuccess);

        // Read back through a fresh context, so this proves the database round trip and not a
        // value still sitting in the change tracker.
        await using var readScope = provider.CreateAsyncScope();
        var station = await readScope.ServiceProvider.GetRequiredService<YcrDbContext>()
            .Stations.AsNoTracking().SingleAsync(CancellationToken);

        Assert.Equal(MyanmarName, station.Name.My);
        Assert.Equal(MyanmarName.Length, station.Name.My.Length);
    }

    /// <summary>
    /// Guards the <c>{}</c> serialisation trap. <c>IAuditWriter.Record</c> takes
    /// <c>IAuditSnapshot</c>, which declares no members, so serialising against the static type
    /// would write an empty object into every payload — a silent, total loss of audit state in a
    /// table that can never be corrected, with no error anywhere.
    /// </summary>
    /// <remarks>
    /// Read straight out of <c>audit.AuditEvents</c> and asserted verbatim, including the
    /// Myanmar name unescaped, so both the runtime-type serialisation and the widened JSON
    /// encoder are covered by the same assertion.
    /// </remarks>
    [Fact]
    public async Task Handle_WithValidCommand_WritesTheStationStateIntoAfterJsonVerbatim()
    {
        const string Code = "INS";
        const string MyanmarName = "အင်းစိန်";
        await using var provider = BuildProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<CreateStationHandler>()
                .Handle(new CreateStationCommand(Code, "Insein", MyanmarName), CancellationToken);
            Assert.True(result.IsSuccess);
        }

        var afterJson = await ScalarAsync<string>(
            "SELECT [AfterJson] FROM [audit].[AuditEvents] WHERE [Action] = N'Network.StationCreated';");

        Assert.NotNull(afterJson);
        Assert.NotEqual("{}", afterJson);
        Assert.Contains(Code, afterJson, StringComparison.Ordinal);
        // Verbatim, not as \uXXXX escapes: an investigator reads this column directly.
        Assert.Contains(MyanmarName, afterJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u", afterJson, StringComparison.Ordinal);
        Assert.Equal(
            """{"code":"INS","nameEn":"Insein","nameMy":"အင်းစိန်","isActive":true}""",
            afterJson);

        // Creation events have no prior state (ADR-0021), which is null rather than "{}".
        Assert.Null(await ScalarAsync<string>(
            "SELECT [BeforeJson] FROM [audit].[AuditEvents] WHERE [Action] = N'Network.StationCreated';"));
    }

    [Fact]
    public async Task Handle_WithDuplicateCode_ReturnsConflict()
    {
        await using var provider = BuildProvider();
        await CreateAsync(provider, "BGO", "Bago");

        var second = await CreateAsync(provider, "BGO", "Bago Again");

        Assert.True(second.IsFailure);
        Assert.Equal("Network.StationCodeAlreadyExists", second.Error.Code);
        Assert.Equal(ErrorType.Conflict, second.Error.Type);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [network].[Stations];"));
    }

    /// <summary>
    /// S6 and R3: a deactivated station keeps its row, so its code can never be reused. The test
    /// proves the retained row is what enforces it, rather than assuming a separate mechanism.
    /// </summary>
    [Fact]
    public async Task Handle_WithCodeOfDeactivatedStation_ReturnsConflict()
    {
        await using var provider = BuildProvider();
        var created = await CreateAsync(provider, "PZD", "Pazundaung");

        await using (var scope = provider.CreateAsyncScope())
        {
            var deactivated = await scope.ServiceProvider.GetRequiredService<DeactivateStationHandler>()
                .Handle(new DeactivateStationCommand(created.Value), CancellationToken);
            Assert.True(deactivated.IsSuccess);
        }

        var reused = await CreateAsync(provider, "PZD", "Pazundaung Reborn");

        Assert.True(reused.IsFailure);
        Assert.Equal("Network.StationCodeAlreadyExists", reused.Error.Code);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [network].[Stations];"));
    }

    /// <summary>
    /// S13: two genuinely concurrent creates of the same code yield exactly one station.
    /// </summary>
    /// <remarks>
    /// Each request gets its own provider, hence its own connection and change tracker, so the
    /// race is real rather than simulated. Which guard fires is deliberately <em>not</em> asserted:
    /// depending on scheduling either the pre-check or the unique index may reject the loser, and
    /// both are correct. The index path specifically is proven by
    /// <c>UniqueConstraintTranslationTests</c>, which provokes the violation directly rather than
    /// hoping the scheduler cooperates.
    /// </remarks>
    [Fact]
    public async Task Handle_WithParallelDuplicateRequests_PersistsExactlyOneStation()
    {
        await using var first = BuildProvider();
        await using var second = BuildProvider();

        var results = await Task.WhenAll(
            CreateAsync(first, "TMW", "Thamaing"),
            CreateAsync(second, "TMW", "Thamaing"));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        var failure = Assert.Single(results, result => result.IsFailure);
        Assert.Equal("Network.StationCodeAlreadyExists", failure.Error.Code);
        Assert.Equal(ErrorType.Conflict, failure.Error.Type);

        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [network].[Stations];"));
        // The losing request's audit row rolls back with its insert: one station, one event.
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents];"));
    }

    /// <summary>
    /// S20: actor fields come from the authenticated context. The command carries no actor field
    /// at all, which is the structural half of the guarantee; the recorded values are the
    /// behavioural half.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRequestSuppliesActorFields_IgnoresThem()
    {
        var realActor = Guid.CreateVersion7();
        await using var provider = BuildProvider(new TestCurrentUser
        {
            UserId = realActor,
            Roles = ["Admin"],
            ClientIp = "203.0.113.9",
            CorrelationId = "corr-real",
            AuthorizedByPermission = Permissions.StationsManage
        });

        // Every string the caller controls is filled with an attempt to impersonate someone else.
        await using (var scope = provider.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<CreateStationHandler>()
                .Handle(
                    new CreateStationCommand("DNN", "actorUserId=00000000-0000-0000-0000-000000000001", "role=Admin"),
                    CancellationToken);
            Assert.True(result.IsSuccess);
        }

        Assert.Equal(realActor, await ScalarAsync<Guid>("SELECT [ActorUserId] FROM [audit].[AuditEvents];"));
        Assert.Equal("""["Admin"]""", await ScalarAsync<string>("SELECT [ActorRole] FROM [audit].[AuditEvents];"));
        Assert.Equal("203.0.113.9", await ScalarAsync<string>("SELECT [ClientIp] FROM [audit].[AuditEvents];"));
        Assert.Equal("corr-real", await ScalarAsync<string>("SELECT [CorrelationId] FROM [audit].[AuditEvents];"));
    }

    [Fact]
    public void CreateStationCommand_ExposesNoActorField()
    {
        // ADR-0017 item 2 is enforced by shape: there is no property through which a request
        // could offer an actor, address or correlation id in the first place.
        var properties = typeof(CreateStationCommand).GetProperties().Select(property => property.Name);

        Assert.Equal(["Code", "NameEn", "NameMy"], properties);
    }

    /// <summary>Spec S9's code cases at the handler, below the endpoint's validation filter.</summary>
    /// <remarks>
    /// F-005-1 added the three the matrix was missing. <c>ABCDEFGHIJK</c> is exactly 11
    /// characters — the boundary itself, where <c>TOOLONGCODE1</c> at 12 only ever proved that
    /// something clearly too long is refused. <c>IN S</c> is an embedded space, which trimming
    /// cannot remove, unlike the whitespace-only case beside it. <c>""</c> is the empty string:
    /// the handler is reached directly here, so unlike at the endpoint there is no filter in
    /// front of it and this asserts the domain rule itself answers.
    /// </remarks>
    [Theory]
    [InlineData("i", "Too Short")]
    [InlineData("ABCDEFGHIJK", "Exactly One Over")]
    [InlineData("TOOLONGCODE1", "Too Long")]
    [InlineData("ins", "Lower Case")]
    [InlineData("IN-S", "Punctuation")]
    [InlineData("IN S", "Embedded Space")]
    [InlineData("", "Empty")]
    [InlineData("  ", "Blank")]
    public async Task Handle_WithInvalidCode_ReturnsValidationError(string code, string nameEn)
    {
        await using var provider = BuildProvider();

        var result = await CreateAsync(provider, code, nameEn);

        Assert.True(result.IsFailure);
        Assert.Equal("Network.InvalidStationCode", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM [network].[Stations];"));
    }

    [Fact]
    public async Task Handle_WithMissingMyanmarName_ReturnsValidationError()
    {
        await using var provider = BuildProvider();

        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<CreateStationHandler>()
            .Handle(new CreateStationCommand("INS", "Insein", "   "), CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("Network.InvalidStationName", result.Error.Code);
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents];"));
    }

    private async Task<Result<Guid>> CreateAsync(IServiceProvider provider, string code, string nameEn)
    {
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider
            .GetRequiredService<CreateStationHandler>()
            .Handle(new CreateStationCommand(code, nameEn, "မြန်မာ"), CancellationToken);
    }
}
