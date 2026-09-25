using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using YCR.Api.Tests.Authentication;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// F-004 S41, S42 and OQ49 end to end with <strong>real</strong> ES256 tokens: the seeded service
/// grants (<c>Identity_SeedServicePermissionGrants</c>) reach the endpoints through F-002's pipeline,
/// under <c>ycr_app</c>. No test handler is involved, so a missing or wrong seed row fails here.
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-25; T-044, OQ49) — not a Myanma
/// Railways answer: <c>services.manage</c> → <c>SystemAdministrator</c>, <c>RailwayAdministrator</c>;
/// <c>services.read</c> → all eight roles. The dates are relative to the test clock's Asia/Yangon
/// date, because this pipeline runs on the real current time.
/// </remarks>
public sealed class ServicePermissionGrantTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    public static TheoryData<string, bool> Roles() => new()
    {
        { RoleNames.SystemAdministrator, true },
        { RoleNames.RailwayAdministrator, true },
        { RoleNames.StationManager, false },
        { RoleNames.TicketOperator, false },
        { RoleNames.TicketInspector, false },
        { RoleNames.FinanceOfficer, false },
        { RoleNames.Auditor, false },
        { RoleNames.ReportingUser, false },
    };

    protected override string DatabasePrefix => "api_service_grants";

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task EveryRole_HasExactlyTheSeededServiceRights(string role, bool manages)
    {
        await using var api = RealApi();
        var (_, admin) = await SignedInAsync(api, "railway.admin", RoleNames.RailwayAdministrator);
        var route = await CreateRouteAsync(admin);
        var (_, caller) = await SignedInAsync(api, "role.user", role);

        Assert.Equal(HttpStatusCode.OK, (await caller.GetAsync("/api/v1/services", CancellationToken)).StatusCode);

        var create = await caller.PostAsJsonAsync("/api/v1/services", Body(route.RouteId, route.Stations), CancellationToken);

        Assert.Equal(manages ? HttpStatusCode.Created : HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(manages ? 1 : 0, await CountAsync("SELECT COUNT(*) FROM [timetable].[Services];"));
        Assert.Equal(manages ? 1 : 0, await AuditCountAsync("Timetable.ServiceCreated"));
    }

    [Fact]
    public async Task RailwayAdministrator_CreatesAndWithdrawsServiceAuditedAsServicesManage()
    {
        await using var api = RealApi();
        var (adminId, admin) = await SignedInAsync(api, "railway.admin", RoleNames.RailwayAdministrator);
        var route = await CreateRouteAsync(admin);

        var create = await admin.PostAsJsonAsync("/api/v1/services", Body(route.RouteId, route.Stations), CancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var serviceId = (await create.Content.ReadFromJsonAsync<IdBody>(CancellationToken))!.Id;
        var withdraw = await admin.PostAsJsonAsync(
            $"/api/v1/services/{serviceId}/withdraw", new { withdrawFrom = Date(30) }, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, withdraw.StatusCode);
        var created = Assert.Single(await AuditRowsAsync("Timetable.ServiceCreated"));
        var withdrawn = Assert.Single(await AuditRowsAsync("Timetable.ServiceWithdrawn"));
        Assert.Equal(adminId, created.ActorUserId);
        Assert.Equal(adminId, withdrawn.ActorUserId);
        Assert.Equal("services.manage", created.AuthorizedByPermission);
        Assert.Equal("services.manage", withdrawn.AuthorizedByPermission);
    }

    /// <summary>A date <paramref name="days"/> after the test clock's Asia/Yangon date.</summary>
    private string Date(int days) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Asia/Yangon")).DateTime)
            .AddDays(days)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private Dictionary<string, object?> Body(Guid routeId, IReadOnlyList<Guid> stations) => new()
    {
        ["code"] = "S101",
        ["nameEn"] = "Circular",
        ["nameMy"] = "မြို့ပတ်ရထား",
        ["routeId"] = routeId.ToString(),
        ["direction"] = "Forward",
        ["stopStationIds"] = stations.Select(id => id.ToString()).ToArray(),
        ["operatingDays"] = new[] { "Monday", "Friday" },
        ["effectiveFrom"] = Date(0),
    };

    private async Task<(Guid RouteId, IReadOnlyList<Guid> Stations)> CreateRouteAsync(HttpClient client)
    {
        var stations = new List<Guid>();
        foreach (var code in new[] { "AAA", "BBB", "CCC" })
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/stations", new { code, nameEn = $"Station {code}", nameMy = "ဘူတာ" }, CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            stations.Add((await response.Content.ReadFromJsonAsync<IdBody>(CancellationToken))!.Id);
        }

        var route = await client.PostAsJsonAsync(
            "/api/v1/routes",
            new
            {
                code = "R1",
                nameEn = "Circular Route",
                nameMy = "မြို့ပတ်ရထားလမ်း",
                isClosed = false,
                stationIds = stations.Select(id => id.ToString()).ToArray(),
            },
            CancellationToken);
        Assert.Equal(HttpStatusCode.Created, route.StatusCode);
        return ((await route.Content.ReadFromJsonAsync<IdBody>(CancellationToken))!.Id, stations);
    }

    private sealed record IdBody(Guid Id);
}
