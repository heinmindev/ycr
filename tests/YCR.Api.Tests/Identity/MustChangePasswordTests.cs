using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using YCR.Api.Tests.Authentication;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// R26 (U2, N1; ADR-0023 item 9): a must-change session may call only <c>/auth/me</c>,
/// <c>/auth/refresh</c>, <c>/auth/logout</c> and <c>/auth/password</c> (spec S19d).
/// </summary>
public sealed class MustChangePasswordTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    private const string NewPassword = "shwe.dagon.2026";

    /// <summary>The routes R26 leaves open to an authenticated must-change session.</summary>
    private static readonly HashSet<string> Allowed = ["GET /api/v1/auth/me", "POST /api/v1/auth/password", "POST /api/v1/auth/logout"];

    protected override string DatabasePrefix => "api_must_change";

    /// <summary>
    /// Every route in the running application that requires authorization — found from the
    /// endpoint table, so an endpoint added later is covered without editing this test — answers
    /// <c>403 Auth.PasswordChangeRequired</c> to a <c>SystemAdministrator</c> who holds every
    /// permission, except the three R26 allows. <c>/auth/refresh</c> is anonymous and is checked
    /// separately.
    /// </summary>
    [Fact]
    public async Task MustChangeSession_MayCallOnlyMeRefreshLogoutAndPassword()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.SystemAdministrator], mustChangePassword: true, cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessToken, refreshToken) = await SignInAsync(client, "hein.min");
        using var bearer = Client(api, accessToken: accessToken);

        var protectedRoutes = ProtectedRoutes(api);
        Assert.Contains("GET /api/v1/stations", protectedRoutes);
        Assert.Superset(Allowed, protectedRoutes.ToHashSet());

        foreach (var route in protectedRoutes.Where(route => !Allowed.Contains(route)))
        {
            var (method, path) = (route.Split(' ')[0], route.Split(' ')[1]);
            using var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal));
            if (method is "POST" or "PUT")
            {
                request.Content = JsonContent.Create(new { });
            }

            var response = await bearer.SendAsync(request, CancellationToken);

            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{route} → {(int)response.StatusCode}");
            Assert.Equal("Auth.PasswordChangeRequired", await ErrorCodeOf(response));
        }

        Assert.Equal(HttpStatusCode.OK, (await bearer.GetAsync("/api/v1/auth/me", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(client, refreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await bearer.PostAsync("/api/v1/auth/logout", content: null, CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task AfterPasswordChange_SameSessionSucceedsWithoutSignIn()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], mustChangePassword: true, cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessToken, _) = await SignInAsync(client, "hein.min");
        using var bearer = Client(api, accessToken: accessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await bearer.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);

        var change = await bearer.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = StaffUserSeeder.Password, newPassword = NewPassword }, CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bearer.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);
        Assert.False(await ScalarAsync<bool>("SELECT [MustChangePassword] FROM [identity].[Users];"));
    }

    [Fact]
    public async Task MustChangeSession_AnonymousEndpoints_AreUnaffected()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], mustChangePassword: true, cancellationToken: CancellationToken);
        using var client = Client(api);
        var (accessToken, _) = await SignInAsync(client, "hein.min");
        using var bearer = Client(api, accessToken: accessToken);

        // N1: sign-in and the health probes are not "authenticated requests" in R26's sense.
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(bearer, "hein.min")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bearer.GetAsync("/health/live", CancellationToken)).StatusCode);
    }

    private static List<string> ProtectedRoutes(YcrApiFactory api) =>
        [.. api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
                && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => $"{method} /{Normalize(endpoint.RoutePattern.RawText)}"))
            .Distinct()
            .Order(StringComparer.Ordinal)];

    /// <summary><c>/api/v1/stations/{id:guid}</c> → <c>api/v1/stations/{id}</c>; no trailing slash.</summary>
    private static string Normalize(string? pattern) =>
        System.Text.RegularExpressions.Regex.Replace(pattern ?? string.Empty, @"\{(\w+)(:[^}]*)?\}", "{$1}").Trim('/');
}
