using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Authorization;

namespace YCR.Api.Tests.Authentication;

/// <summary>
/// Hosts the real <c>Program</c> against the pinned container, with only authentication
/// substituted.
/// </summary>
/// <remarks>
/// Everything else — the permission policy provider, ProblemDetails, the exception handler, the
/// endpoints and the least-privilege <c>ycr_app</c> connection — is the production wiring, so a
/// test failure here means the deployed API would fail the same way.
/// </remarks>
/// <param name="connectionString">The least-privilege <c>ycr_app</c> connection.</param>
/// <param name="environment">The hosting environment; <c>Production</c> exercises S21b.</param>
/// <param name="registerTestAuthentication">
/// <see langword="false"/> hosts the application exactly as a deployment does — with no
/// authentication scheme at all. Every other test registers the test handler, which is precisely
/// why they could not see that an unauthenticated request used to produce a 500.
/// </param>
public sealed class YcrApiFactory(
    string connectionString,
    string environment = "Testing",
    bool registerTestAuthentication = true)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Application", connectionString);

        if (!registerTestAuthentication)
        {
            return;
        }

        // ADR-0020 item 2: the test handler reaches the application only here, never from src/.
        builder.ConfigureTestServices(services => services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
    }

    /// <summary>A client carrying no credential at all.</summary>
    public HttpClient CreateAnonymousClient() => CreateClient();

    /// <summary>A client authenticated as a caller holding exactly <paramref name="permissions"/>.</summary>
    public HttpClient CreateClientWith(params string[] permissions) =>
        CreateClientAs(Guid.CreateVersion7(), permissions);

    /// <summary>A client holding both station permissions, which most tests want.</summary>
    public HttpClient CreateManagerClient() =>
        CreateClientWith(Permissions.StationsManage, Permissions.StationsRead);

    public HttpClient CreateClientAs(Guid userId, params string[] permissions)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissions));
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "StationManager");

        return client;
    }
}
