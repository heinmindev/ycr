using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YCR.Api.Tests.Authentication;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Identity;

/// <summary>
/// S28 (R13; <c>docs/20</c> §7): after the sign-in scenarios, no log line, audit row or
/// ProblemDetails body carries a password, a password hash, an access or refresh token or the
/// cookie value, and no log line carries a username.
/// </summary>
public sealed class SecretLeakTests(SqlServerFixture fixture) : RealAuthApiTestBase(fixture)
{
    private const string NewPassword = "shwe.dagon.2026";
    private const string WrongPassword = "wrong.password.1";
    private const string RejectedPassword = "qwertyuiop12";

    protected override string DatabasePrefix => "api_secret_leak";

    [Fact]
    public async Task AfterAuthScenarios_NoSecretInLogsAuditOrProblemBodies_NoUserNameInLogs()
    {
        var logs = new CapturingLoggerProvider();
        await using var api = RealApi(
            new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Trace" },
            configureServices: services => services.AddSingleton<ILoggerProvider>(logs));
        await StaffUserSeeder.SeedAsync(api, "hein.min", [RoleNames.StationManager], cancellationToken: CancellationToken);
        using var client = Client(api);
        var secrets = new List<string> { StaffUserSeeder.Password, NewPassword, WrongPassword, RejectedPassword };
        var problems = new List<string>();

        async Task<HttpResponseMessage> Track(Task<HttpResponseMessage> call)
        {
            var response = await call;
            if (!response.IsSuccessStatusCode)
            {
                problems.Add(await response.Content.ReadAsStringAsync(CancellationToken));
            }

            return response;
        }

        // Sign-in: wrong, unknown (the typed name must not reach a log either), right.
        await Track(LoginAsync(client, "hein.min", WrongPassword));
        await Track(LoginAsync(client, "ghost.user", WrongPassword));
        var (accessA, refreshA) = await SignInAsync(client, "hein.min");
        var (accessB, refreshB) = await SignInAsync(client, "hein.min");
        secrets.AddRange([accessA, refreshA, accessB, refreshB]);

        // Refresh: rotate, predecessor inside grace (409), ancestor (family revoked), garbage.
        var rotated = await Track(RefreshAsync(client, refreshB));
        var refreshB2 = RefreshCookieOf(rotated)!;
        secrets.AddRange([await AccessTokenOf(rotated), refreshB2]);
        await Track(RefreshAsync(client, refreshB));
        var refreshB3 = RefreshCookieOf(await Track(RefreshAsync(client, refreshB2)))!;
        secrets.Add(refreshB3);
        await Track(RefreshAsync(client, refreshB));
        await Track(RefreshAsync(client, "garbage-token"));

        // Own password: wrong current, policy, success; a bad token; logout.
        using var bearer = Client(api, accessToken: accessA);
        await Track(bearer.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = WrongPassword, newPassword = NewPassword }, CancellationToken));
        await Track(bearer.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = StaffUserSeeder.Password, newPassword = RejectedPassword }, CancellationToken));
        Assert.Equal(HttpStatusCode.NoContent, (await Track(bearer.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = StaffUserSeeder.Password, newPassword = NewPassword }, CancellationToken))).StatusCode);
        await Track(bearer.GetAsync("/api/v1/auth/me", CancellationToken));
        using var tampered = Client(api, accessToken: accessA[..^4] + "AAAA");
        await Track(tampered.GetAsync("/api/v1/auth/me", CancellationToken));
        await Track(bearer.PostAsync("/api/v1/auth/logout", content: null, CancellationToken));

        var hash = await ScalarAsync<string>("SELECT [PasswordHash] FROM [identity].[Users];");
        var stamp = await ScalarAsync<string>("SELECT [SecurityStamp] FROM [identity].[Users];");
        secrets.AddRange([hash!, stamp!]);

        Assert.NotEmpty(logs.Lines);
        Assert.True(problems.Count >= 8, $"Only {problems.Count} problem bodies were captured.");
        var audit = (await AuditRowsAsync()).Select(row => string.Join('|', row.ActorRole, row.BeforeJson, row.AfterJson, row.SubjectType)).ToList();
        Assert.NotEmpty(audit);

        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(logs.Lines, line => line.Contains(secret, StringComparison.Ordinal));
            Assert.DoesNotContain(audit, row => row.Contains(secret, StringComparison.Ordinal));
            Assert.DoesNotContain(problems, body => body.Contains(secret, StringComparison.Ordinal));
        }

        foreach (var userName in new[] { "hein.min", "ghost.user" })
        {
            Assert.DoesNotContain(logs.Lines, line => line.Contains(userName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> lines = new();

        public IReadOnlyCollection<string> Lines => lines;

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, lines);

        public void Dispose()
        {
        }

        private sealed class Logger(string category, ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                lines.Enqueue($"{category} scope: {state}");
                return null;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                ArgumentNullException.ThrowIfNull(formatter);
                var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? string.Join(", ", pairs.Select(pair => $"{pair.Key}={pair.Value}"))
                    : string.Empty;
                lines.Enqueue($"{logLevel} {category}: {formatter(state, exception)} [{properties}] {exception}");
            }
        }
    }
}
