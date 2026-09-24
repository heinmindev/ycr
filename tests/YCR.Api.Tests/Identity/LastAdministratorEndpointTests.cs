using System.Net;
using YCR.Api.Tests.Authentication;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// R27 (U5; provisional tech-lead ruling — not a Myanma Railways answer): at least one active
/// <c>SystemAdministrator</c> remains, including under concurrent requests (spec S19e).
/// </summary>
public sealed class LastAdministratorEndpointTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_last_admin";

    [Fact]
    public async Task DisableAndReEnableSecondAdministrator_AllowedWhileOneRemains()
    {
        await using var api = RealApi();
        var (_, adminA) = await SignedInAsync(api, "admin.a", RoleNames.SystemAdministrator);
        var adminB = await StaffUserSeeder.SeedAsync(api, "admin.b", [RoleNames.SystemAdministrator], cancellationToken: CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, (await adminA.PostAsync($"/api/v1/users/{adminB}/disable", content: null, CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminA.PostAsync($"/api/v1/users/{adminB}/enable", content: null, CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminA.PostAsync($"/api/v1/users/{adminB}/disable", content: null, CancellationToken)).StatusCode);

        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM [identity].[Users] WHERE [IsDisabled] = 0;"));
    }

    /// <summary>
    /// Two active administrators each disable the other at the same moment: exactly one succeeds,
    /// the other gets <c>422 Identity.LastAdministrator</c>, and one active administrator remains.
    /// </summary>
    [Fact]
    public async Task MutualDisableAtOnce_ExactlyOneSucceeds()
    {
        await using var api = RealApi();
        var (idA, adminA) = await SignedInAsync(api, "admin.a", RoleNames.SystemAdministrator);
        var (idB, adminB) = await SignedInAsync(api, "admin.b", RoleNames.SystemAdministrator);

        var responses = await Task.WhenAll(
            adminA.PostAsync($"/api/v1/users/{idB}/disable", content: null, CancellationToken),
            adminB.PostAsync($"/api/v1/users/{idA}/disable", content: null, CancellationToken));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        var refused = Assert.Single(responses, response => response.StatusCode == (HttpStatusCode)422);
        Assert.Equal("Identity.LastAdministrator", await ErrorCodeOf(refused));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM [identity].[Users] WHERE [IsDisabled] = 0;"));
    }
}
