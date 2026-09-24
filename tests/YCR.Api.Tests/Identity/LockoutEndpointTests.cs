using System.Net;
using YCR.Api.Tests.Authentication;
using YCR.Application.Identity;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>Lockout through the sign-in endpoint (spec S4; R18, R23; ADR-0023 item 3).</summary>
/// <remarks>The administrator-unlock half arrives with the administration endpoints (plan step 9).</remarks>
public sealed class LockoutEndpointTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_lockout";

    [Fact]
    public async Task Login_NineFailuresThenSuccess_ResetsTheCount()
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);

        for (var attempt = 0; attempt < 9; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, "hein.min", "wrong.password.1")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "hein.min")).StatusCode);
        Assert.Equal((0, (DateTimeOffset?)null), await LockoutStateAsync(userId));
    }

    [Fact]
    public async Task Login_TenFailures_LocksForFifteenMinutesThenSucceeds()
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var failure = await LoginAsync(client, "hein.min", "wrong.password.1");
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
            Assert.Equal("Auth.InvalidCredentials", await ErrorCodeOf(failure));
        }

        Assert.Equal(10, (await AuditRowsAsync(IdentityAuditActions.LoginFailed)).Count);
        var lockedOut = Assert.Single(await AuditRowsAsync(IdentityAuditActions.LockedOut));
        Assert.Equal(userId, lockedOut.SubjectId);
        Assert.Null(lockedOut.ActorUserId);
        Assert.Equal(Clock.GetUtcNow().AddMinutes(15), (await LockoutStateAsync(userId)).LockoutEnd);

        // Locked: the correct password is refused like any other failure, and writes one LoginFailed.
        Clock.Advance(TimeSpan.FromMinutes(15) - TimeSpan.FromSeconds(1));
        var whileLocked = await LoginAsync(client, "hein.min");
        Assert.Equal(HttpStatusCode.Unauthorized, whileLocked.StatusCode);
        Assert.Equal("Auth.InvalidCredentials", await ErrorCodeOf(whileLocked));
        Assert.Equal(11, (await AuditRowsAsync(IdentityAuditActions.LoginFailed)).Count);

        Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "hein.min")).StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUserTenTimes_LocksNothingAndStoresNoTypedValue()
    {
        await using var api = RealApi();
        using var client = Client(api);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, "typed.value.x", "typed.password.x")).StatusCode);
        }

        var rows = await AuditRowsAsync(IdentityAuditActions.LoginFailed);
        Assert.Equal(10, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Null(row.SubjectId);
            Assert.Null(row.BeforeJson);
            Assert.Null(row.AfterJson);
        });
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.LockedOut));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE CONCAT([BeforeJson], [AfterJson], [SubjectType], [ActorRole], [ReasonCode]) LIKE N'%typed%';"));
    }
}
