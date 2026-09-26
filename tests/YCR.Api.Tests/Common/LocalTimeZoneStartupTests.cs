using YCR.Api.Tests.Authentication;

namespace YCR.Api.Tests.Common;

/// <summary>
/// F-004 plan P11, ruling Q3: the API refuses to start without a <c>Time:LocalTimeZone</c> this
/// host can resolve, and starts with the shipped <c>Asia/Yangon</c>.
/// </summary>
/// <remarks>
/// The <c>SigningKeyStartupTests</c> pattern, on the <see cref="AuthMode.Unmodified"/> host:
/// nothing substituted, only configuration, the path a deployment's settings take.
/// </remarks>
public sealed class LocalTimeZoneStartupTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_local_time_zone";

    [Theory]
    [InlineData("absent", null)]
    [InlineData("empty", "")]
    [InlineData("unknown", "Mars/Olympus")]
    public async Task Startup_WithMissingOrUnknownLocalTimeZone_FailsWithClearError(string _, string? zone)
    {
        await using var api = WithZone(zone);

        var failure = Assert.ThrowsAny<Exception>(() => api.CreateAnonymousClient());

        var messages = string.Join(" | ", Chain(failure).Select(exception => exception.Message));
        Assert.Contains("Time:LocalTimeZone", messages, StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(zone))
        {
            Assert.Contains($"'{zone}'", messages, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Startup_WithAsiaYangon_Starts()
    {
        await using var api = WithZone("Asia/Yangon");

        var response = await api.CreateAnonymousClient().GetAsync("/health/live", CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
    }

    /// <summary>A null value removes the shipped setting, as a deployment that omits it would.</summary>
    private YcrApiFactory WithZone(string? zone) =>
        new(
            Database.ApplicationConnectionString,
            mode: AuthMode.Unmodified,
            settings: new Dictionary<string, string?> { ["Time:LocalTimeZone"] = zone });

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
