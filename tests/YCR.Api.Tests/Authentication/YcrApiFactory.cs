using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Authorization;
using YCR.TestSupport;

namespace YCR.Api.Tests.Authentication;

/// <summary>How a test host authenticates (F-002 plan step 7; D13).</summary>
public enum AuthMode
{
    /// <summary>
    /// The F-001 test handler is the default scheme, for API tests that are not about
    /// authentication. <c>Testing</c> environment only (ADR-0020 as amended).
    /// </summary>
    TestHandler,

    /// <summary>
    /// The production pipeline — <c>JwtBearerHandler</c>, real ES256 tokens, per-request principal
    /// resolution — with only the clock substituted when a test supplies one.
    /// </summary>
    RealTokens,

    /// <summary>
    /// Nothing substituted at all: no <c>ConfigureTestServices</c>; configuration only (S25, S22).
    /// </summary>
    Unmodified,
}

/// <summary>
/// Hosts the real <c>Program</c> against the pinned container.
/// </summary>
/// <remarks>
/// Everything that is not substituted — the permission policy provider, ProblemDetails, the
/// exception handler, the endpoints, the least-privilege <c>ycr_app</c> connection — is the
/// production wiring. Configuration reaches the host through <c>UseSetting</c>, the same path a
/// deployment's environment variables take: an ephemeral ES256 key (never on disk), an allowed
/// origin, and any per-test overrides. Clients use <c>https://localhost</c>, so the
/// <c>Secure</c> refresh cookie round-trips (V8, step 8).
/// </remarks>
public sealed class YcrApiFactory : WebApplicationFactory<Program>
{
    public const string AllowedOrigin = "https://ycr.test";

    private readonly string connectionString;
    private readonly string environment;
    private readonly AuthMode mode;
    private readonly IReadOnlyDictionary<string, string?> settings;
    private readonly TestSigningKey signingKey;

    /// <param name="connectionString">The least-privilege <c>ycr_app</c> connection.</param>
    /// <param name="environment">The hosting environment; <c>Production</c> exercises the startup guards.</param>
    /// <param name="mode">How requests are authenticated.</param>
    /// <param name="clock">A test clock for token lifetimes and the principal cache, or null for the system clock.</param>
    /// <param name="settings">Extra configuration, applied last.</param>
    /// <param name="signingKey">The signing key; a fresh <c>test-</c> key when null.</param>
    public YcrApiFactory(
        string connectionString,
        string environment = "Testing",
        AuthMode mode = AuthMode.TestHandler,
        TestClock? clock = null,
        IReadOnlyDictionary<string, string?>? settings = null,
        TestSigningKey? signingKey = null)
    {
        this.connectionString = connectionString;
        this.environment = environment;
        this.mode = mode;
        this.settings = settings ?? new Dictionary<string, string?>();
        this.signingKey = signingKey ?? new TestSigningKey();
        Clock = clock;
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    public TestClock? Clock { get; }

    public TestSigningKey SigningKey => signingKey;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Application", connectionString);
        foreach (var (key, value) in signingKey.ToConfiguration())
        {
            builder.UseSetting(key, value);
        }

        builder.UseSetting("Auth:AllowedOrigins:0", AllowedOrigin);
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        if (mode == AuthMode.Unmodified)
        {
            return;
        }

        builder.ConfigureTestServices(services =>
        {
            if (Clock is not null)
            {
                services.AddSingleton<TimeProvider>(Clock);
            }

            if (mode == AuthMode.TestHandler)
            {
                // ADR-0020 item 2: the test handler reaches the application only here, never from src/.
                services
                    .AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            }
        });
    }

    /// <summary>A client carrying no credential at all.</summary>
    public HttpClient CreateAnonymousClient() => CreateClient();

    /// <summary>A client authenticated as a caller holding exactly <paramref name="permissions"/> (test handler).</summary>
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

    /// <summary>A client sending <paramref name="accessToken"/> as a bearer token.</summary>
    public HttpClient CreateBearerClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            signingKey.Dispose();
        }
    }
}
