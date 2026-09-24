using System.Net;
using YCR.Api.Tests.Authentication;
using YCR.Application.Identity;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// R9 (ADR-0016 §Browser security; <c>docs/18</c> required control): the three cookie endpoints
/// refuse a missing or foreign <c>Origin</c> before any credential or token is evaluated (S6, S12, S15).
/// </summary>
public sealed class OriginTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_origin";

    private const string NoOrigin = "(none)";

    /// <summary>Missing, foreign, the sandboxed <c>null</c>, another port, another scheme, a trailing path, a look-alike host.</summary>
    public static TheoryData<string> RejectedOrigins() =>
        [NoOrigin, "https://evil.test", "null", "https://ycr.test:8443", "http://ycr.test", "https://ycr.test/", "https://ycr.test.evil.test"];

    [Theory]
    [MemberData(nameof(RejectedOrigins))]
    public async Task Login_MissingOrForeignOrigin_Returns403WithNoCookieNoAuditNoCount(string origin)
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api, OriginOrNull(origin));

        var correct = await LoginAsync(client, "hein.min");
        var wrong = await LoginAsync(client, "hein.min", "wrong.password.1");

        foreach (var response in new[] { correct, wrong })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("Auth.OriginRejected", await ErrorCodeOf(response));
            Assert.Null(RefreshSetCookie(response));
        }

        Assert.Equal(0, await AuditCountAsync());
        Assert.Equal((0, (DateTimeOffset?)null), await LockoutStateAsync(userId));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM [identity].[AuthSessions];"));
    }

    [Fact]
    public async Task Login_AllowedOriginInOtherCase_IsAccepted()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api, "HTTPS://YCR.TEST");

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "hein.min")).StatusCode);
    }

    [Theory]
    [MemberData(nameof(RejectedOrigins))]
    public async Task Refresh_MissingOrForeignOrigin_Returns403WithoutRotation(string origin)
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var allowed = Client(api);
        var (_, refreshToken) = await SignInAsync(allowed, "hein.min");
        using var client = Client(api, OriginOrNull(origin));

        var response = await RefreshAsync(client, refreshToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Auth.OriginRejected", await ErrorCodeOf(response));
        Assert.Null(RefreshSetCookie(response));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE [RotatedAtUtc] IS NOT NULL;"));

        // The token was not consumed: from the right origin it still rotates.
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(allowed, refreshToken)).StatusCode);
    }

    [Theory]
    [MemberData(nameof(RejectedOrigins))]
    public async Task Logout_MissingOrigin_Returns403AndRevokesNothing(string origin)
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var allowed = Client(api);
        var (accessToken, _) = await SignInAsync(allowed, "hein.min");
        using var client = Client(api, OriginOrNull(origin), accessToken: accessToken);

        var response = await client.PostAsync("/api/v1/auth/logout", content: null, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Auth.OriginRejected", await ErrorCodeOf(response));
        Assert.Null(RefreshSetCookie(response));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [RevokedAtUtc] IS NOT NULL;"));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.LoggedOut));
    }

    private static string? OriginOrNull(string origin) => origin == NoOrigin ? null : origin;
}
