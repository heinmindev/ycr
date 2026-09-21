using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using YCR.Api.Contracts.Network;
using YCR.Application.Common.Authorization;

namespace YCR.Api.Tests.Network;

/// <summary>Spec S1–S14 end to end through the real API under the <c>ycr_app</c> credential.</summary>
public sealed class StationEndpointsTests(SqlServerFixture fixture) : ApiTestBase(fixture)
{
    protected override string DatabasePrefix => "api_stations";

    [Fact]
    public async Task Post_WithValidRequest_Returns201WithLocation()
    {
        using var client = Api.CreateManagerClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/stations",
            new CreateStationRequest("INS", "Insein", "အင်းစိန်"),
            CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateStationResponse>(CancellationToken);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal($"/api/v1/stations/{created.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Get_ReturnsResponseRecordNotEntity()
    {
        using var client = Api.CreateManagerClient();
        var id = await CreateAsync(client, "YGN", "Yangon Central", "ရန်ကုန်ဘူတာကြီး");

        var response = await client.GetAsync($"/api/v1/stations/{id}", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var station = await response.Content.ReadFromJsonAsync<StationResponse>(CancellationToken);
        Assert.NotNull(station);
        Assert.Equal("YGN", station.Code);
        Assert.Equal("ရန်ကုန်ဘူတာကြီး", station.NameMy);
        Assert.True(station.IsActive);

        // AGENTS.md rule 4: the wire shape is the response contract, so none of the entity's own
        // surface leaks. `domainEvents` is the give-away an EF entity was serialised.
        var json = await response.Content.ReadAsStringAsync(CancellationToken);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            ["id", "code", "nameEn", "nameMy", "isActive", "createdAtUtc"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task Deactivate_WhenActive_Returns204_AndWhenInactive_Returns422()
    {
        using var client = Api.CreateManagerClient();
        var id = await CreateAsync(client, "BGO", "Bago", "ပဲခူး");

        var first = await client.PostAsync($"/api/v1/stations/{id}/deactivate", null, CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.PostAsync($"/api/v1/stations/{id}/deactivate", null, CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableContent, second.StatusCode);
        Assert.Equal("Network.StationAlreadyInactive", await ErrorCodeOf(second));
    }

    [Fact]
    public async Task Post_WithDuplicateCode_Returns409WithErrorCode()
    {
        using var client = Api.CreateManagerClient();
        await CreateAsync(client, "PZD", "Pazundaung", "ပုဇွန်တောင်");

        var response = await client.PostAsJsonAsync(
            "/api/v1/stations",
            new CreateStationRequest("PZD", "Pazundaung Again", "ပုဇွန်တောင်"),
            CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Network.StationCodeAlreadyExists", await ErrorCodeOf(response));
    }

    [Fact]
    public async Task Get_WithUnknownId_Returns404()
    {
        using var client = Api.CreateManagerClient();

        var response = await client.GetAsync($"/api/v1/stations/{Guid.CreateVersion7()}", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Network.StationNotFound", await ErrorCodeOf(response));
    }

    [Fact]
    public async Task List_ReturnsPagedEnvelope()
    {
        using var client = Api.CreateManagerClient();
        await CreateAsync(client, "AAA", "Alpha", "မြန်မာ");
        await CreateAsync(client, "BBB", "Bravo", "မြန်မာ");

        var response = await client.GetAsync("/api/v1/stations?page=1&pageSize=50", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<StationResponse>>(CancellationToken);
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(["AAA", "BBB"], page.Items.Select(station => station.Code));
    }

    [Fact]
    public async Task List_WithPageSizeAbove200_Returns400()
    {
        using var client = Api.CreateManagerClient();

        var response = await client.GetAsync("/api/v1/stations?page=1&pageSize=201", CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Network.InvalidPageRequest", await ErrorCodeOf(response));
    }

    [Theory]
    [InlineData("i", "Insein", "အင်းစိန်")]
    [InlineData("TOOLONGCODE1", "Insein", "အင်းစိန်")]
    [InlineData("ins", "Insein", "အင်းစိန်")]
    [InlineData("IN-S", "Insein", "အင်းစိန်")]
    public async Task Post_WithInvalidCode_Returns400ProblemDetails(string code, string nameEn, string nameMy)
    {
        using var client = Api.CreateManagerClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/stations",
            new CreateStationRequest(code, nameEn, nameMy),
            CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Network.InvalidStationCode", await ErrorCodeOf(response));
    }

    [Theory]
    [InlineData(null, "Insein", "အင်းစိန်")]
    [InlineData("INS", null, "အင်းစိန်")]
    [InlineData("INS", "Insein", null)]
    [InlineData("", "Insein", "အင်းစိန်")]
    [InlineData("INS", "Insein", "   ")]
    public async Task Post_WithMissingField_Returns400FromTheValidationFilter(string? code, string? nameEn, string? nameMy)
    {
        using var client = Api.CreateManagerClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/stations",
            new CreateStationRequest(code, nameEn, nameMy),
            CancellationToken);

        // The filter rejects a missing field before the handler runs; a present-but-invalid
        // field is the handler's business rule. Both are 400, so a caller cannot tell which
        // layer answered — only that the request was wrong.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Common.ValidationFailed", await ErrorCodeOf(response));
    }

    /// <summary>
    /// A name that is present but too long reaches the domain rule, which is the half of the
    /// validation the endpoint filter deliberately does not duplicate.
    /// </summary>
    /// <remarks>
    /// Overlong rather than whitespace: FluentValidation's <c>NotEmpty()</c> already treats a
    /// whitespace-only string as empty, so <c>"   "</c> never gets past the filter. That case is
    /// covered by the filter theory above; this one proves R4's length rule is still enforced
    /// from the endpoint, and that it is enforced in <c>BilingualName</c> alone.
    /// </remarks>
    [Fact]
    public async Task Post_WithOverlongMyanmarName_Returns400FromTheDomainRule()
    {
        using var client = Api.CreateManagerClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/stations",
            new CreateStationRequest("INS", "Insein", new string('မ', 101)),
            CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Network.InvalidStationName", await ErrorCodeOf(response));
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var client = Api.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/stations",
            new CreateStationRequest("INS", "Insein", "အင်းစိန်"),
            CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = Api.CreateAnonymousClient();

        var response = await client.GetAsync("/api/v1/stations", CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithoutStationsManage_Returns403()
    {
        // Holds stations.read only: authenticated, but not authorised for a write. This is the
        // case a role check would get wrong, which is why permissions are per action.
        using var client = Api.CreateClientWith(Permissions.StationsRead);

        var response = await client.PostAsJsonAsync(
            "/api/v1/stations",
            new CreateStationRequest("INS", "Insein", "အင်းစိန်"),
            CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_WithoutStationsManage_Returns403()
    {
        using var manager = Api.CreateManagerClient();
        var id = await CreateAsync(manager, "SHW", "Shwedagon", "ရွှေတိဂုံ");

        using var reader = Api.CreateClientWith(Permissions.StationsRead);
        var response = await reader.PostAsync($"/api/v1/stations/{id}/deactivate", null, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutStationsRead_Returns403()
    {
        using var client = Api.CreateClientWith(Permissions.StationsManage);

        var response = await client.GetAsync("/api/v1/stations", CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// S20 end to end: a request that tries to supply actor fields cannot influence the audit
    /// row. The body has nowhere to put them, so the attempt goes in the header instead.
    /// </summary>
    [Fact]
    public async Task Post_WhenRequestTriesToSupplyActorFields_RecordsTheAuthenticatedActor()
    {
        var realActor = Guid.CreateVersion7();
        using var client = Api.CreateClientAs(realActor, Permissions.StationsManage);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");
        client.DefaultRequestHeaders.Add("ActorUserId", Guid.Empty.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/v1/stations",
            new CreateStationRequest("DNN", "Danyingon", "ဒညင်းကုန်း"),
            CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.Equal(realActor, await AuditScalarAsync<Guid>("SELECT [ActorUserId] FROM [audit].[AuditEvents];"));
        // Derived from the endpoint's own policy, not from anything the caller sent.
        Assert.Equal(
            Permissions.StationsManage,
            await AuditScalarAsync<string>("SELECT [AuthorizedByPermission] FROM [audit].[AuditEvents];"));
        // The forged X-Forwarded-For is ignored: the address is the connection's, as the server saw it.
        Assert.NotEqual("10.0.0.1", await AuditScalarAsync<string>("SELECT [ClientIp] FROM [audit].[AuditEvents];"));
    }

    private async Task<Guid> CreateAsync(HttpClient client, string code, string nameEn, string nameMy)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/stations",
            new CreateStationRequest(code, nameEn, nameMy),
            CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateStationResponse>(CancellationToken);

        return created!.Id;
    }

    private async Task<string?> ErrorCodeOf(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync(CancellationToken);
        using var document = JsonDocument.Parse(json);

        // docs/20 §4: every error carries errorCode and traceId.
        Assert.True(document.RootElement.TryGetProperty("traceId", out _), json);

        return document.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    private async Task<T?> AuditScalarAsync<T>(string sql)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(Database.MigratorConnectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(CancellationToken);

        return value is null or DBNull ? default : (T)value;
    }
}
