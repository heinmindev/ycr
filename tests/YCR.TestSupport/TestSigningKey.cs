using System.Security.Cryptography;

namespace YCR.TestSupport;

/// <summary>
/// An ephemeral ES256 (P-256) signing key, generated per test run and never written to disk
/// (ADR-0023 item 7; spec R14: keys are never committed).
/// </summary>
/// <remarks>
/// Exposed as the configuration values the API binds under <c>Auth:Signing</c>
/// (plan F-002 §Affected files, <c>SigningKeyOptions</c>), so a test host receives its key through
/// the same path as a deployment. The <c>kid</c> starts <c>test-</c>, which the production
/// startup check refuses (S26a).
/// </remarks>
public sealed class TestSigningKey : IDisposable
{
    public TestSigningKey(string? keyId = null)
    {
        Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        KeyId = keyId ?? $"test-{Guid.NewGuid():N}";
        PrivateKeyPkcs8Pem = Key.ExportPkcs8PrivateKeyPem();
    }

    public string KeyId { get; }

    public ECDsa Key { get; }

    public string PrivateKeyPkcs8Pem { get; }

    /// <summary>
    /// The <c>Auth:Signing</c> configuration for this key as the only, active key.
    /// </summary>
    public IReadOnlyDictionary<string, string?> ToConfiguration(bool developmentOnly = true) =>
        new Dictionary<string, string?>
        {
            ["Auth:Signing:ActiveKeyId"] = KeyId,
            ["Auth:Signing:Keys:0:KeyId"] = KeyId,
            ["Auth:Signing:Keys:0:PrivateKeyPkcs8Pem"] = PrivateKeyPkcs8Pem,
            ["Auth:Signing:Keys:0:DevelopmentOnly"] = developmentOnly ? "true" : "false",
        };

    public void Dispose() => Key.Dispose();
}
