using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YCR.Api.Common.Authorization;

namespace YCR.Api.Tests.Authentication;

/// <summary>
/// The only <c>AuthenticationHandler</c> in this repository (ADR-0020 item 2).
/// </summary>
/// <remarks>
/// It lives in the test project and is registered only through
/// <c>WebApplicationFactory.ConfigureTestServices</c>. It must never move into <c>src/</c>:
/// spec S21a asserts no <c>AuthenticationHandler&lt;&gt;</c> subtype exists there, and S21b has
/// startup refuse an unexpected scheme outside the <c>Testing</c> environment.
/// <para>
/// It stands in for the ADR-0016 token validation that ADR-0020 defers to a later feature.
/// Reading the principal from request headers is exactly what a real handler would do with a
/// bearer token — the difference is that this one does not verify anything, which is why it is
/// confined to tests.
/// </para>
/// </remarks>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserIdHeader = "X-Test-UserId";
    public const string PermissionsHeader = "X-Test-Permissions";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // No header means an anonymous caller. NoResult rather than Fail, so the endpoint
        // challenges with 401 instead of reporting an authentication error (spec S11).
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userId) || string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        List<Claim> claims = [new(ClaimTypes.NameIdentifier, userId.ToString())];

        claims.AddRange(Split(PermissionsHeader)
            .Select(permission => new Claim(PermissionAuthorizationHandler.PermissionClaimType, permission)));
        claims.AddRange(Split(RolesHeader).Select(role => new Claim(ClaimTypes.Role, role)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    private string[] Split(string header) =>
        Request.Headers.TryGetValue(header, out var value) && !string.IsNullOrWhiteSpace(value)
            ? [.. value.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
            : [];
}
