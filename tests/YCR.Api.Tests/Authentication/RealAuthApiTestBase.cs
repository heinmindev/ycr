using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using YCR.TestSupport;

namespace YCR.Api.Tests.Authentication;

/// <summary>
/// Shared setup for the real-token API suites (plan step 8): hosts in <see cref="AuthMode.RealTokens"/>,
/// clients that send an allowed <c>Origin</c> and handle the refresh cookie explicitly, and ledger
/// and table readers under the migrator credential.
/// </summary>
/// <remarks>
/// <para>
/// The clock starts at the current wall-clock second, not at <see cref="TestClock"/>'s fixed
/// default: an <c>HttpClient</c> cookie container judges the cookie's <c>Expires</c> against real
/// time, and by the fixed default the session would have ended "yesterday", so the cookie would
/// never be sent. Tests that move the clock send cookies explicitly.
/// </para>
/// <para>
/// Spec §4: unless a scenario says otherwise, rate limits are set high enough not to interfere;
/// the limit suites opt back into the shipped defaults. The principal cache runs at the 30-second
/// ceiling, so a "within 30 s" assertion advances the clock by exactly the bound (plan P4).
/// </para>
/// </remarks>
public abstract class RealAuthApiTestBase(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    protected TestClock Clock { get; } = new(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

    /// <summary>The session lifetime D12 fixes.</summary>
    protected static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);

    protected YcrApiFactory RealApi(
        IReadOnlyDictionary<string, string?>? settings = null,
        bool shippedRateLimits = false,
        Action<IServiceCollection>? configureServices = null,
        AuthMode mode = AuthMode.RealTokens,
        string environment = "Testing")
    {
        var merged = new Dictionary<string, string?> { ["Auth:PrincipalCacheSeconds"] = "30" };
        if (!shippedRateLimits)
        {
            merged["Auth:RateLimits:LoginPerUserNamePerMinute"] = "10000";
            merged["Auth:RateLimits:LoginPerClientAddressPerMinute"] = "10000";
            merged["Auth:RateLimits:RefreshPerClientAddressPerMinute"] = "10000";
        }

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            merged[key] = value;
        }

        // Outside Development and Testing, startup refuses a test- or development key (S26a).
        var signingKey = environment is "Development" or "Testing" ? null : new TestSigningKey($"prod-{Guid.NewGuid():N}", developmentOnly: false);
        return new YcrApiFactory(Database.ApplicationConnectionString, environment, mode, Clock, merged, signingKey, configureServices);
    }

    /// <summary>
    /// A client that sends <paramref name="origin"/> (the allowed one by default; null sends none),
    /// optionally "from" <paramref name="clientAddress"/>, and does not keep cookies.
    /// </summary>
    protected static HttpClient Client(
        YcrApiFactory api,
        string? origin = YcrApiFactory.AllowedOrigin,
        string? clientAddress = null,
        string? accessToken = null)
    {
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        if (origin is not null)
        {
            client.DefaultRequestHeaders.Add("Origin", origin);
        }

        if (clientAddress is not null)
        {
            client.DefaultRequestHeaders.Add(YcrApiFactory.ClientAddressHeader, clientAddress);
        }

        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }

    protected static Task<HttpResponseMessage> LoginAsync(HttpClient client, string? userName, string? password = StaffUserSeeder.Password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password }, CancellationToken);

    /// <summary>POSTs <c>/auth/refresh</c> carrying <paramref name="refreshToken"/> as the cookie (none when null).</summary>
    protected static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string? refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        if (refreshToken is not null)
        {
            request.Headers.Add("Cookie", $"ycr_refresh={refreshToken}");
        }

        return client.SendAsync(request, CancellationToken);
    }

    /// <summary>Signs in over HTTP and returns the access token and the refresh cookie value.</summary>
    protected static async Task<(string AccessToken, string RefreshToken)> SignInAsync(HttpClient client, string userName, string password = StaffUserSeeder.Password)
    {
        var response = await LoginAsync(client, userName, password);
        response.EnsureSuccessStatusCode();
        return (await AccessTokenOf(response), RefreshCookieOf(response) ?? throw new InvalidOperationException("No refresh cookie."));
    }

    /// <summary>Seeds a user holding <paramref name="roles"/>, signs them in over HTTP and returns a bearer client.</summary>
    protected static async Task<(Guid UserId, HttpClient Client)> SignedInAsync(YcrApiFactory api, string userName, params string[] roles)
    {
        var userId = await StaffUserSeeder.SeedAsync(api, userName, roles, cancellationToken: CancellationToken);
        using var anonymous = Client(api);
        var (accessToken, _) = await SignInAsync(anonymous, userName);
        return (userId, Client(api, accessToken: accessToken));
    }

    protected static async Task<string> AccessTokenOf(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    /// <summary>The <c>ycr_refresh</c> <c>Set-Cookie</c> header, or null.</summary>
    protected static string? RefreshSetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.SingleOrDefault(value => value.StartsWith("ycr_refresh=", StringComparison.Ordinal))
            : null;

    /// <summary>The value the <c>ycr_refresh</c> cookie is set to, or null.</summary>
    protected static string? RefreshCookieOf(HttpResponseMessage response) =>
        RefreshSetCookie(response) is { } header ? header["ycr_refresh=".Length..header.IndexOf(';', StringComparison.Ordinal)] : null;

    protected static async Task<string?> ErrorCodeOf(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        return body.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    protected static Guid SessionOf(string accessToken)
    {
        using var payload = JsonDocument.Parse(System.Buffers.Text.Base64Url.DecodeFromChars(accessToken.Split('.')[1]));
        return Guid.Parse(payload.RootElement.GetProperty("sid").GetString()!);
    }

    protected async Task ExecuteAsMigratorAsync(string sql)
    {
        await using var connection = new SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken);
    }

    protected async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(CancellationToken);
        return value is null or DBNull ? default : (T)value;
    }

    protected Task<int> CountAsync(string sql) => ScalarAsync<int>(sql)!;

    protected Task<int> AuditCountAsync(string? action = null) =>
        CountAsync(action is null
            ? "SELECT COUNT(*) FROM [audit].[AuditEvents];"
            : $"SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Action] = N'{action}';");

    /// <summary>Every ledger row for <paramref name="action"/>, oldest first (every row when null).</summary>
    protected async Task<List<AuditRow>> AuditRowsAsync(string? action = null)
    {
        await using var connection = new SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(
            """
            SELECT [Action], [ActorUserId], [ActorRole], [SubjectType], [SubjectId], [BeforeJson], [AfterJson], [AuthorizedByPermission], [ClientIp]
            FROM [audit].[AuditEvents] WHERE @action IS NULL OR [Action] = @action ORDER BY [OccurredAtUtc], [Id];
            """,
            connection);
        command.Parameters.Add(new SqlParameter("@action", System.Data.SqlDbType.NVarChar, 100) { Value = (object?)action ?? DBNull.Value });
        await using var reader = await command.ExecuteReaderAsync(CancellationToken);
        var rows = new List<AuditRow>();
        while (await reader.ReadAsync(CancellationToken))
        {
            rows.Add(new AuditRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return rows;
    }

    protected async Task<(int Count, DateTimeOffset? LockoutEnd)> LockoutStateAsync(Guid userId)
    {
        await using var connection = new SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new SqlCommand(
            $"SELECT [AccessFailedCount], [LockoutEndUtc] FROM [identity].[Users] WHERE [Id] = '{userId}';", connection);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken);
        Assert.True(await reader.ReadAsync(CancellationToken));
        return (reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetDateTimeOffset(1));
    }

    public sealed record AuditRow(
        string Action,
        Guid? ActorUserId,
        string? ActorRole,
        string SubjectType,
        Guid? SubjectId,
        string? BeforeJson,
        string? AfterJson,
        string? AuthorizedByPermission,
        string? ClientIp);
}
