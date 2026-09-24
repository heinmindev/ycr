using System.Net;
using System.Net.Http.Json;
using YCR.Api.Tests.Authentication;
using YCR.Application.Identity;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// Review S-1 (ADR-0023 item 4, amended 2026-09-24): in Production, creating a user with, or
/// granting, <c>SystemAdministrator</c>, <c>RailwayAdministrator</c> or <c>FinanceOfficer</c> is
/// refused with <c>422 Identity.PrivilegedRoleRequiresMfa</c> and changes nothing; the same requests
/// succeed in Development and Testing. The bootstrap CLI's refusal is in
/// <c>BootstrapAdministratorCommandTests</c>.
/// </summary>
public sealed class PrivilegedRoleGateEndpointTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    private const string NewPassword = "shwe.dagon.2026";

    protected override string DatabasePrefix => "api_privileged_gate";

    public static TheoryData<string> PrivilegedRoles() => [.. PrivilegedRoleGate.PrivilegedRoles];

    [Theory]
    [MemberData(nameof(PrivilegedRoles))]
    public async Task Production_CreateWithPrivilegedRole_Returns422AndCreatesNothing(string role)
    {
        await using var api = RealApi(environment: "Production");
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);

        var response = await admin.PostAsJsonAsync(
            "/api/v1/users", new { userName = "new.user", password = NewPassword, roles = new[] { RoleNames.Auditor, role } }, CancellationToken);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("Identity.PrivilegedRoleRequiresMfa", await ErrorCodeOf(response));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM [identity].[Users];"));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.UserCreated));
    }

    [Theory]
    [MemberData(nameof(PrivilegedRoles))]
    public async Task Production_GrantPrivilegedRole_Returns422AndChangesNothing(string role)
    {
        await using var api = RealApi(environment: "Production");
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        var clerkId = await StaffUserSeeder.SeedAsync(api, "ticket.clerk", [RoleNames.TicketOperator], cancellationToken: CancellationToken);

        var response = await admin.PutAsJsonAsync($"/api/v1/users/{clerkId}/roles", new { roles = new[] { RoleNames.TicketOperator, role } }, CancellationToken);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("Identity.PrivilegedRoleRequiresMfa", await ErrorCodeOf(response));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.RolesChanged));
        Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM [identity].[UserRoles] WHERE [UserId] = '{clerkId}';"));
    }

    /// <summary>The gate refuses only privileged roles: ordinary provisioning still works in Production.</summary>
    [Fact]
    public async Task Production_CreateAndGrantUnprivilegedRoles_Succeed()
    {
        await using var api = RealApi(environment: "Production");
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);

        var create = await admin.PostAsJsonAsync(
            "/api/v1/users", new { userName = "new.user", password = NewPassword, roles = new[] { RoleNames.TicketOperator } }, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<IdResponse>(CancellationToken))!.Id;

        var grant = await admin.PutAsJsonAsync($"/api/v1/users/{id}/roles", new { roles = new[] { RoleNames.StationManager, RoleNames.Auditor } }, CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
    }

    public static TheoryData<string, string> NonProductionCases()
    {
        var cases = new TheoryData<string, string>();
        foreach (var environment in new[] { "Development", "Testing" })
        {
            foreach (var role in PrivilegedRoleGate.PrivilegedRoles)
            {
                cases.Add(environment, role);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(NonProductionCases))]
    public async Task NonProduction_CreateAndGrantPrivilegedRole_Succeed(string environment, string role)
    {
        await using var api = RealApi(environment: environment);
        var (_, admin) = await SignedInAsync(api, "admin.user", RoleNames.SystemAdministrator);
        var clerkId = await StaffUserSeeder.SeedAsync(api, "ticket.clerk", [RoleNames.TicketOperator], cancellationToken: CancellationToken);

        var create = await admin.PostAsJsonAsync(
            "/api/v1/users", new { userName = "new.user", password = NewPassword, roles = new[] { role } }, CancellationToken);
        var grant = await admin.PutAsJsonAsync($"/api/v1/users/{clerkId}/roles", new { roles = new[] { RoleNames.TicketOperator, role } }, CancellationToken);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.UserCreated));
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.RolesChanged));
    }

    private sealed record IdResponse(Guid Id);
}
