using System.Net;
using System.Net.Http.Json;
using YCR.Api.Tests.Authentication;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// S26 (<c>docs/21</c> §Tests): an authenticated real user whose only role is <c>TicketOperator</c>
/// is refused station writes and every administration endpoint.
/// </summary>
public sealed class WrongPermissionTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_wrong_permission";

    [Fact]
    public async Task TicketOperator_PostStations_Returns403()
    {
        await using var api = RealApi();
        var (_, operatorClient) = await SignedInAsync(api, "ticket.operator", RoleNames.TicketOperator);

        var response = await operatorClient.PostAsJsonAsync("/api/v1/stations", new { code = "YGN", nameEn = "Yangon", nameMy = "ရန်ကုန်" }, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await operatorClient.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
    }

    [Theory]
    [MemberData(nameof(UserAdministrationEndpointTests.AdministrationEndpoints), MemberType = typeof(UserAdministrationEndpointTests))]
    public async Task TicketOperator_AnyAdministrationEndpoint_Returns403(string method, string path)
    {
        await using var api = RealApi();
        var (_, operatorClient) = await SignedInAsync(api, "ticket.operator", RoleNames.TicketOperator);
        var target = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{id}", target.ToString(), StringComparison.Ordinal));
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { userName = "x.y.z", password = "shwe.dagon.2026", newPassword = "shwe.dagon.2026", roles = new[] { RoleNames.SystemAdministrator } });
        }

        var response = await operatorClient.SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] NOT IN (N'Identity.LoginSucceeded');"));
    }
}
