using System.Net;
using System.Text.Json;
using YCR.Application.Common.Authorization;

namespace YCR.Api.Tests.Common;

/// <summary>
/// Spec S25: an unexpected failure returns 500 and tells the caller nothing about the inside of
/// the system.
/// </summary>
public sealed class ProblemDetailsTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_problems";

    /// <summary>
    /// Provoked by pointing the API at a database that does not exist, so the failure is a real
    /// unhandled infrastructure exception travelling the real pipeline — not a test endpoint
    /// written to throw, which would prove only that the handler catches what it was given.
    /// </summary>
    [Fact]
    public async Task UnhandledException_Returns500WithoutInternalDetail()
    {
        await using var broken = new Authentication.YcrApiFactory(
            Database.ApplicationConnectionString.Replace(
                Database.Name, $"{Database.Name}_missing", StringComparison.Ordinal));

        using var client = broken.CreateClientWith(Permissions.StationsRead);
        var response = await client.GetAsync($"/api/v1/stations/{Guid.CreateVersion7()}", CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(CancellationToken);

        // REQUIRED CONTROL (docs/18, data disclosure): no exception type, no stack trace, no SQL,
        // no server name, and above all no credential from the connection string.
        Assert.DoesNotContain("SqlException", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User Id", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ycr_app", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at YCR.", body, StringComparison.Ordinal);
        Assert.DoesNotContain(Database.Name, body, StringComparison.OrdinalIgnoreCase);

        // What it does carry: a stable code and a trace id an operator can follow into the logs.
        using var document = JsonDocument.Parse(body);
        Assert.Equal("Common.UnexpectedError", document.RootElement.GetProperty("errorCode").GetString());
        Assert.False(
            string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ErrorResponses_AreProblemDetailsJson()
    {
        using var client = Api.CreateClientWith(Permissions.StationsRead);

        var response = await client.GetAsync($"/api/v1/stations/{Guid.CreateVersion7()}", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
