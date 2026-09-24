using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using YCR.Api.Tests.Authentication;
using YCR.Domain.Identity;
using YCR.TestSupport;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// Access tokens through the real pipeline (spec S13, S20, S21; plan P3, P11, V3, R-2): tokens are
/// minted by the production issuer for sessions the production login started, and validated by
/// the framework <c>JwtBearerHandler</c> with the principal rebuilt from the database.
/// </summary>
public sealed class AccessTokenTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    private readonly TestClock clock = new();

    protected override string DatabasePrefix => "api_access_tokens";

    [Fact]
    public async Task Token_HasEs256KidAndExactlyEightClaims()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);

        var signIn = await StaffUserSeeder.SignInAsync(api, "hein.min", CancellationToken);

        var parts = signIn.AccessToken.Split('.');
        Assert.Equal(3, parts.Length);
        using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
        Assert.Equal("ES256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal(api.SigningKey.KeyId, header.RootElement.GetProperty("kid").GetString());
        using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        Assert.Equal(
            ["aud", "exp", "iat", "iss", "jti", "nbf", "sid", "sub"],
            payload.RootElement.EnumerateObject().Select(claim => claim.Name).Order(StringComparer.Ordinal));
        Assert.Equal(clock.GetUtcNow().AddMinutes(15).ToUnixTimeSeconds(), payload.RootElement.GetProperty("exp").GetInt64());
        Assert.Equal(clock.GetUtcNow().AddMinutes(15), signIn.AccessTokenExpiresAtUtc);
    }

    [Fact]
    public async Task Token_Valid_GrantsExactlyTheRolesPermissions()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        var signIn = await StaffUserSeeder.SignInAsync(api, "hein.min", CancellationToken);
        using var client = api.CreateBearerClient(signIn.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);

        // StationManager holds stations.read only (R11), so creating a station is forbidden.
        var create = await client.PostAsync("/api/v1/stations", JsonContent("""{"code":"YGN","nameEn":"Yangon","nameMy":"ရန်ကုန်"}"""), CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    public static TheoryData<string> InvalidTokenCases() =>
        ["tampered", "wrong-key", "hs256", "none", "wrong-iss", "wrong-aud", "revoked-sid", "unknown-sid", "sub-not-session-user", "no-sid"];

    /// <summary>S21: every way a token can be wrong ends in the same 401 Auth.Unauthenticated.</summary>
    [Theory]
    [MemberData(nameof(InvalidTokenCases))]
    public async Task Token_Invalid_Returns401(string defect)
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        var other = await StaffUserSeeder.SeedAsync(api, "other.user", [RoleNames.StationManager], cancellationToken: CancellationToken);
        var signIn = await StaffUserSeeder.SignInAsync(api, "hein.min", CancellationToken);
        var sessionId = SessionOf(signIn.AccessToken);

        var token = defect switch
        {
            "tampered" => Tamper(signIn.AccessToken),
            "wrong-key" => Mint(api, userId, sessionId, credentials: OtherEs256Key(api.SigningKey.KeyId)),
            "hs256" => Mint(api, userId, sessionId, credentials: new SigningCredentials(
                new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)) { KeyId = api.SigningKey.KeyId }, SecurityAlgorithms.HmacSha256)),
            "none" => Unsigned(userId, sessionId),
            "wrong-iss" => Mint(api, userId, sessionId, issuer: "someone.else"),
            "wrong-aud" => Mint(api, userId, sessionId, audience: "someone.else"),
            "revoked-sid" => await RevokeAndReturnAsync(sessionId, signIn.AccessToken),
            "unknown-sid" => Mint(api, userId, Guid.NewGuid()),
            "sub-not-session-user" => Mint(api, other, sessionId),
            "no-sid" => Mint(api, userId, sessionId: null),
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };

        await AssertUnauthenticatedAsync(api, token);
    }

    /// <summary>S13 / V3: expiry is judged on the injected clock, with P11's 30-second skew.</summary>
    [Fact]
    public async Task Token_PastExp_Returns401()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        var signIn = await StaffUserSeeder.SignInAsync(api, "hein.min", CancellationToken);
        using var client = api.CreateBearerClient(signIn.AccessToken);

        clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(29));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);

        clock.Advance(TimeSpan.FromSeconds(1));
        await AssertUnauthenticatedAsync(api, signIn.AccessToken);
    }

    /// <summary>S13: an unexpired token whose session has passed its 12-hour absolute lifetime.</summary>
    [Fact]
    public async Task Token_UnexpiredButSessionPastLifetime_Returns401()
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        var signIn = await StaffUserSeeder.SignInAsync(api, "hein.min", CancellationToken);
        clock.Advance(TimeSpan.FromHours(12) - TimeSpan.FromMinutes(5));
        var lateToken = Mint(api, userId, SessionOf(signIn.AccessToken));
        using var client = api.CreateBearerClient(lateToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/stations", CancellationToken)).StatusCode);

        clock.Advance(TimeSpan.FromMinutes(5));

        await AssertUnauthenticatedAsync(api, lateToken);
    }

    /// <summary>R-2 / P3: a permission or role claim inside a validly signed token grants nothing.</summary>
    [Fact]
    public async Task Token_WithForgedPermissionClaim_GrantsNothing()
    {
        await using var api = RealApi();
        var userId = await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        var signIn = await StaffUserSeeder.SignInAsync(api, "hein.min", CancellationToken);
        var forged = Mint(api, userId, SessionOf(signIn.AccessToken), extraClaims: new Dictionary<string, object>
        {
            ["permission"] = "stations.manage",
            ["role"] = RoleNames.SystemAdministrator,
            ["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] = RoleNames.SystemAdministrator,
        });
        using var client = api.CreateBearerClient(forged);

        var create = await client.PostAsync("/api/v1/stations", JsonContent("""{"code":"YGN","nameEn":"Yangon","nameMy":"ရန်ကုန်"}"""), CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    /// <summary>R26 / N1: a must-change session is refused on protected endpoints; anonymous ones are unaffected.</summary>
    [Fact]
    public async Task MustChangeSession_ProtectedEndpoint_Returns403PasswordChangeRequired()
    {
        await using var api = RealApi();
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.SystemAdministrator], mustChangePassword: true, cancellationToken: CancellationToken);
        var signIn = await StaffUserSeeder.SignInAsync(api, "hein.min", CancellationToken);
        using var client = api.CreateBearerClient(signIn.AccessToken);

        var response = await client.GetAsync("/api/v1/stations", CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal("Auth.PasswordChangeRequired", body.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", CancellationToken)).StatusCode);
    }

    private YcrApiFactory RealApi() =>
        new(Database.ApplicationConnectionString, mode: AuthMode.RealTokens, clock: clock);

    private async Task AssertUnauthenticatedAsync(YcrApiFactory api, string token)
    {
        using var client = api.CreateBearerClient(token);
        var response = await client.GetAsync("/api/v1/stations", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal("Auth.Unauthenticated", body.RootElement.GetProperty("errorCode").GetString());
    }

    private string Mint(
        YcrApiFactory api,
        Guid userId,
        Guid? sessionId,
        SigningCredentials? credentials = null,
        string issuer = "YCR.Api",
        string audience = "YCR.Api",
        IDictionary<string, object>? extraClaims = null)
    {
        var now = clock.GetUtcNow();
        var claims = new Dictionary<string, object>
        {
            ["sub"] = userId.ToString(),
            ["jti"] = Guid.NewGuid().ToString("N"),
        };
        if (sessionId is { } sid)
        {
            claims["sid"] = sid.ToString();
        }

        foreach (var (key, value) in extraClaims ?? new Dictionary<string, object>())
        {
            claims[key] = value;
        }

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(15).UtcDateTime,
            Claims = claims,
            SigningCredentials = credentials
                ?? new SigningCredentials(new ECDsaSecurityKey(api.SigningKey.Key) { KeyId = api.SigningKey.KeyId }, SecurityAlgorithms.EcdsaSha256),
        });
    }

    private static SigningCredentials OtherEs256Key(string sameKeyId) =>
        new(new ECDsaSecurityKey(ECDsa.Create(ECCurve.NamedCurves.nistP256)) { KeyId = sameKeyId }, SecurityAlgorithms.EcdsaSha256);

    private string Unsigned(Guid userId, Guid sessionId)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        var header = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            $$"""{"sub":"{{userId}}","sid":"{{sessionId}}","iss":"YCR.Api","aud":"YCR.Api","iat":{{now}},"nbf":{{now}},"exp":{{now + 900}}}"""));
        return $"{header}.{payload}.";
    }

    private static string Tamper(string token)
    {
        var parts = token.Split('.');
        var payload = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[1]));
        var altered = payload.Replace("\"sub\":\"", "\"sub\":\"0", StringComparison.Ordinal);
        return $"{parts[0]}.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(altered))}.{parts[2]}";
    }

    private async Task<string> RevokeAndReturnAsync(Guid sessionId, string token)
    {
        await using var connection = new SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(
            $"UPDATE [identity].[AuthSessions] SET [RevokedAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC', [RevocationReason] = N'AdministratorRevoked' WHERE [Id] = '{sessionId}';",
            connection);
        await command.ExecuteNonQueryAsync(CancellationToken);
        return token;
    }

    private static Guid SessionOf(string token)
    {
        using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(token.Split('.')[1]));
        return Guid.Parse(payload.RootElement.GetProperty("sid").GetString()!);
    }

    private static StringContent JsonContent(string json) => new(json, Encoding.UTF8, "application/json");
}
