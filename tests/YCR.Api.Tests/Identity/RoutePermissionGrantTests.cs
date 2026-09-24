using System.Net;
using System.Net.Http.Json;
using YCR.Api.Tests.Authentication;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// F-003 S15 and OQ40 end to end with <strong>real</strong> ES256 tokens: the seeded route grants
/// (<c>Identity_SeedRoutePermissionGrants</c>) reach the endpoints through F-002's pipeline, under
/// <c>ycr_app</c>. No test handler is involved, so a missing or wrong seed row fails here.
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-24; T-032, OQ40) — not a Myanma
/// Railways answer: <c>routes.manage</c> → <c>SystemAdministrator</c>, <c>RailwayAdministrator</c>;
/// <c>routes.read</c> → all eight roles.
/// </remarks>
public sealed class RoutePermissionGrantTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_route_grants";

    [Fact]
    public async Task TicketOperator_ReadsRoutesButCannotCreateOrDeactivate()
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "railway.admin", RoleNames.RailwayAdministrator);
        var stations = await CreateStationsAsync(admin, "AAA", "BBB");
        var routeId = await CreateRouteAsync(admin, "R1", stations);
        var (_, ticketOperator) = await SignedInAsync(api, "ticket.operator", RoleNames.TicketOperator);

        Assert.Equal(HttpStatusCode.OK, (await ticketOperator.GetAsync("/api/v1/routes", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ticketOperator.GetAsync($"/api/v1/routes/{routeId}", CancellationToken)).StatusCode);

        var create = await ticketOperator.PostAsJsonAsync("/api/v1/routes", Body("R2", stations), CancellationToken);
        var deactivate = await ticketOperator.PostAsync($"/api/v1/routes/{routeId}/deactivate", null, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deactivate.StatusCode);
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM [network].[Routes] WHERE [IsActive] = 1;"));
    }

    [Fact]
    public async Task RailwayAdministrator_CreatesAndDeactivatesRoute()
    {
        await using var api = RealApi();
        var (adminId, admin) = await SignedInAsync(api, "railway.admin", RoleNames.RailwayAdministrator);
        var stations = await CreateStationsAsync(admin, "AAA", "BBB", "CCC");

        var routeId = await CreateRouteAsync(admin, "R1", stations);
        var deactivate = await admin.PostAsync($"/api/v1/routes/{routeId}/deactivate", null, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        var created = Assert.Single(await AuditRowsAsync("Network.RouteCreated"));
        var deactivated = Assert.Single(await AuditRowsAsync("Network.RouteDeactivated"));
        Assert.Equal(adminId, created.ActorUserId);
        Assert.Equal(adminId, deactivated.ActorUserId);
        Assert.Equal("routes.manage", created.AuthorizedByPermission);
        Assert.Equal("routes.manage", deactivated.AuthorizedByPermission);
    }

    private static Dictionary<string, object?> Body(string code, IEnumerable<Guid> stationIds) => new()
    {
        ["code"] = code,
        ["nameEn"] = "Circular Route",
        ["nameMy"] = "မြို့ပတ်ရထားလမ်း",
        ["isClosed"] = false,
        ["stationIds"] = stationIds.Select(id => id.ToString()).ToArray(),
    };

    private async Task<Guid[]> CreateStationsAsync(HttpClient client, params string[] codes)
    {
        var ids = new List<Guid>();
        foreach (var code in codes)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/stations",
                new { code, nameEn = $"Station {code}", nameMy = "ဘူတာ" },
                CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            ids.Add((await response.Content.ReadFromJsonAsync<IdBody>(CancellationToken))!.Id);
        }

        return [.. ids];
    }

    private async Task<Guid> CreateRouteAsync(HttpClient client, string code, IEnumerable<Guid> stationIds)
    {
        var response = await client.PostAsJsonAsync("/api/v1/routes", Body(code, stationIds), CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdBody>(CancellationToken))!.Id;
    }

    private sealed record IdBody(Guid Id);
}
