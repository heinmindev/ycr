using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using YCR.Api.Tests.Authentication;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// F-005 SV46 and OQ59 end to end with <strong>real</strong> ES256 tokens: the seeded schedule
/// grants (<c>Identity_SeedSchedulePermissionGrants</c>) reach the endpoints through F-002's
/// pipeline, under <c>ycr_app</c>. No test handler is involved, so a missing or wrong seed row fails here.
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-26; T-053, OQ59) — not a Myanma
/// Railways answer: <c>schedules.manage</c> → <c>SystemAdministrator</c>, <c>RailwayAdministrator</c>;
/// <c>schedules.read</c> → all eight roles. The versions here list no services and start after
/// today (R50), so no network is needed; dates are relative to the test clock's Asia/Yangon date,
/// because this pipeline runs on the real current time.
/// </remarks>
public sealed class SchedulePermissionGrantTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
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

    protected override string DatabasePrefix => "api_schedule_grants";

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task EveryRole_HasExactlyTheSeededScheduleRights(string role, bool manages)
    {
        await using var api = RealApi();
        var (_, caller) = await SignedInAsync(api, "role.user", role);

        Assert.Equal(HttpStatusCode.OK, (await caller.GetAsync("/api/v1/schedules/versions", CancellationToken)).StatusCode);

        var create = await caller.PostAsJsonAsync("/api/v1/schedules/versions", EmptyVersion(Date(7)), CancellationToken);

        Assert.Equal(manages ? HttpStatusCode.Created : HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(manages ? 1 : 0, await CountAsync("SELECT COUNT(*) FROM [timetable].[ScheduleVersions];"));
        Assert.Equal(manages ? 1 : 0, await AuditCountAsync("Timetable.ScheduleVersionCreated"));
    }

    [Fact]
    public async Task RailwayAdministrator_CreatesPublishesAndCancels_AuditedAsSchedulesManage()
    {
        await using var api = RealApi();
        var (adminId, admin) = await SignedInAsync(api, "railway.admin", RoleNames.RailwayAdministrator);

        var create = await admin.PostAsJsonAsync("/api/v1/schedules/versions", EmptyVersion(Date(7)), CancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<IdBody>(CancellationToken))!.Id;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/schedules/versions/{id}/publish", null, CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/schedules/versions/{id}/cancel", null, CancellationToken)).StatusCode);

        foreach (var action in new[] { "Timetable.ScheduleVersionCreated", "Timetable.ScheduleVersionPublished", "Timetable.ScheduleVersionCancelled" })
        {
            var row = Assert.Single(await AuditRowsAsync(action));
            Assert.Equal(adminId, row.ActorUserId);
            Assert.Equal("schedules.manage", row.AuthorizedByPermission);
            Assert.Equal("Timetable.ScheduleVersion", row.SubjectType);
            Assert.Equal(id, row.SubjectId);
        }
    }

    /// <summary>A date <paramref name="days"/> after the test clock's Asia/Yangon date.</summary>
    private string Date(int days) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Asia/Yangon")).DateTime)
            .AddDays(days)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Dictionary<string, object?> EmptyVersion(string effectiveFrom) => new()
    {
        ["nameEn"] = "Suspension",
        ["nameMy"] = "ရပ်ဆိုင်း",
        ["effectiveFrom"] = effectiveFrom,
        ["services"] = Array.Empty<object>(),
    };

    private sealed record IdBody(Guid Id);
}
