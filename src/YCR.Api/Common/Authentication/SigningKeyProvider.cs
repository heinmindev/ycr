using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace YCR.Api.Common.Authentication;

/// <summary>
/// The ES256 keys that sign and verify access tokens (D11; ADR-0023 item 7; spec R14).
/// </summary>
/// <remarks>
/// An abstraction because the production storage provider is blocked on hosting (like OQ32): the
/// configuration-backed provider is what ships in F-002, and a key-vault provider can replace it
/// without touching issuance or validation.
/// </remarks>
public interface ISigningKeyProvider
{
    /// <summary>The active key, with its <c>kid</c>, for signing.</summary>
    SigningCredentials SigningCredentials { get; }

    /// <summary>Every configured key, so tokens signed by a key being rotated out still verify.</summary>
    IReadOnlyList<SecurityKey> ValidationKeys { get; }
}

/// <summary>
/// Reads the keys from <c>Auth:Signing</c> and refuses to start with a key that is not fit for
/// the environment (S26a; the ADR-0014 "production refuses a known development key" pattern).
/// </summary>
/// <remarks>
/// Refused everywhere: no active key; a <c>kid</c> missing, repeated or not matching the active
/// one; a key that is not a P-256 private key. Refused outside Development and Testing: a key
/// marked <c>DevelopmentOnly</c>, or a <c>kid</c> starting <c>dev-</c> or <c>test-</c>. The error
/// messages name the <c>kid</c>, never key material.
/// </remarks>
public sealed class ConfigurationSigningKeyProvider : ISigningKeyProvider, IDisposable
{
    private const string P256Oid = "1.2.840.10045.3.1.7";

    private readonly List<ECDsa> keys = [];

    public ConfigurationSigningKeyProvider(IOptions<AuthOptions> options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        var signing = options.Value.Signing;
        var relaxed = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        if (signing.Keys.Count == 0 || string.IsNullOrWhiteSpace(signing.ActiveKeyId))
        {
            throw Refuse("no signing key is configured (Auth:Signing:ActiveKeyId and Auth:Signing:Keys). "
                + "Developers generate their own P-256 key and store it with dotnet user-secrets (README).");
        }

        var securityKeys = new List<SecurityKey>();
        foreach (var entry in signing.Keys)
        {
            var keyId = entry.KeyId;
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw Refuse("a signing key has no KeyId.");
            }

            if (securityKeys.Any(existing => existing.KeyId == keyId))
            {
                throw Refuse($"the KeyId '{keyId}' is configured twice.");
            }

            if (!relaxed && (entry.DevelopmentOnly
                || keyId.StartsWith("dev-", StringComparison.OrdinalIgnoreCase)
                || keyId.StartsWith("test-", StringComparison.OrdinalIgnoreCase)))
            {
                throw Refuse($"the key '{keyId}' is a development or test key, which only Development and Testing may use (S26a).");
            }

            securityKeys.Add(new ECDsaSecurityKey(LoadP256(keyId, entry.PrivateKeyPkcs8Pem)) { KeyId = keyId });
        }

        var active = securityKeys.SingleOrDefault(key => key.KeyId == signing.ActiveKeyId)
            ?? throw Refuse($"the active KeyId '{signing.ActiveKeyId}' is not among the configured keys.");

        SigningCredentials = new SigningCredentials(active, SecurityAlgorithms.EcdsaSha256);
        ValidationKeys = securityKeys;
    }

    public SigningCredentials SigningCredentials { get; }

    public IReadOnlyList<SecurityKey> ValidationKeys { get; }

    public void Dispose()
    {
        foreach (var key in keys)
        {
            key.Dispose();
        }
    }

    private ECDsa LoadP256(string keyId, string? pem)
    {
        var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(pem);
            var parameters = key.ExportParameters(includePrivateParameters: true);
            if (key.KeySize != 256 || parameters.Curve.Oid.Value != P256Oid || parameters.D is null)
            {
                throw Refuse($"the key '{keyId}' is not a P-256 private key (ES256 requires P-256).");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            key.Dispose();
            throw Refuse($"the key '{keyId}' is not a PKCS#8 PEM P-256 private key (ES256 requires P-256).");
        }
        catch
        {
            key.Dispose();
            throw;
        }

        keys.Add(key);
        return key;
    }

    private static InvalidOperationException Refuse(string reason) =>
        new($"Refusing to start: {reason} (ADR-0023 item 7; F-002 spec R14.)");
}
