using System.Security.Cryptography;
using YCR.Api.Tests.Authentication;
using YCR.TestSupport;

namespace YCR.Api.Tests.Common;

/// <summary>
/// S26a / R14 (D11; ADR-0023 item 7): startup refuses a signing key that is missing, not P-256,
/// or a development key outside Development and Testing — and P4: a principal-cache TTL above
/// 30 seconds (R3).
/// </summary>
public sealed class SigningKeyStartupTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_signing_keys";

    [Fact]
    public async Task Production_WithDevelopmentOnlyKey_FailsStartup()
    {
        await using var api = Production(new TestSigningKey($"prod-{Guid.NewGuid():N}", developmentOnly: true));

        AssertStartupRefused(api, "development or test key");
    }

    [Theory]
    [InlineData("dev-hein")]
    [InlineData("test-run")]
    [InlineData("DEV-upper")]
    public async Task Production_WithDevOrTestKid_FailsStartup(string keyId)
    {
        await using var api = Production(new TestSigningKey(keyId, developmentOnly: false));

        AssertStartupRefused(api, "development or test key");
    }

    [Fact]
    public async Task Testing_WithDevelopmentKey_Starts()
    {
        await using var api = new YcrApiFactory(Database.ApplicationConnectionString, mode: AuthMode.RealTokens);

        var response = await api.CreateAnonymousClient().GetAsync("/health/live", CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Startup_WithNoActiveKey_FailsStartup()
    {
        await using var api = new YcrApiFactory(
            Database.ApplicationConnectionString,
            mode: AuthMode.RealTokens,
            settings: new Dictionary<string, string?> { ["Auth:Signing:ActiveKeyId"] = "someone-else" });

        AssertStartupRefused(api, "active KeyId");
    }

    [Fact]
    public async Task Startup_WithNonP256Key_FailsStartup()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        using var rsa = RSA.Create(2048);

        foreach (var pem in new[] { p384.ExportPkcs8PrivateKeyPem(), rsa.ExportPkcs8PrivateKeyPem(), "not a key" })
        {
            await using var api = new YcrApiFactory(
                Database.ApplicationConnectionString,
                mode: AuthMode.RealTokens,
                settings: new Dictionary<string, string?> { ["Auth:Signing:Keys:0:PrivateKeyPkcs8Pem"] = pem });

            AssertStartupRefused(api, "P-256");
        }
    }

    /// <summary>R3 / P4: the revocation bound is enforced at startup, not by convention.</summary>
    [Theory]
    [InlineData("31")]
    [InlineData("0")]
    public async Task PrincipalCacheTtlOutsideOneToThirtySeconds_FailsStartup(string seconds)
    {
        await using var api = new YcrApiFactory(
            Database.ApplicationConnectionString,
            mode: AuthMode.RealTokens,
            settings: new Dictionary<string, string?> { ["Auth:PrincipalCacheSeconds"] = seconds });

        AssertStartupRefused(api, "PrincipalCacheSeconds");
    }

    [Fact]
    public async Task Production_WithoutAllowedHttpsOrigin_FailsStartup()
    {
        await using var api = Production(
            new TestSigningKey($"prod-{Guid.NewGuid():N}", developmentOnly: false),
            new Dictionary<string, string?> { ["Auth:AllowedOrigins:0"] = "http://ycr.test" });

        AssertStartupRefused(api, "AllowedOrigins");
    }

    private YcrApiFactory Production(TestSigningKey key, IReadOnlyDictionary<string, string?>? settings = null) =>
        new(Database.ApplicationConnectionString, environment: "Production", mode: AuthMode.Unmodified, settings: settings, signingKey: key);

    private static void AssertStartupRefused(YcrApiFactory api, string expected)
    {
        var failure = Assert.ThrowsAny<Exception>(() => api.CreateAnonymousClient());

        var messages = string.Join(" | ", Chain(failure).Select(exception => exception.Message));
        Assert.Contains(expected, messages, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN PRIVATE KEY", messages, StringComparison.Ordinal);
    }

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.SelectMany(Chain))
                {
                    yield return inner;
                }
            }
        }
    }
}
