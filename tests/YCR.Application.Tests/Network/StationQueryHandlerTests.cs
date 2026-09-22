using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Pagination;
using YCR.Application.Network.CreateStation;
using YCR.Application.Network.GetStation;
using YCR.Application.Network.ListStations;
using YCR.Domain.Common;

namespace YCR.Application.Tests.Network;

/// <summary>Spec S3, S4, S8 and S10 against real SQL Server under <c>ycr_app</c>.</summary>
public sealed class StationQueryHandlerTests(SqlServerFixture fixture) : NetworkHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "station_queries";

    [Fact]
    public async Task Handle_WithKnownId_ReturnsDtoNotEntity()
    {
        await using var provider = BuildProvider();
        var stationId = await CreateAsync(provider, "INS", "Insein", "အင်းစိန်");

        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<GetStationHandler>()
            .Handle(new GetStationQuery(stationId), CancellationToken);

        Assert.True(result.IsSuccess);
        // AGENTS.md rule 4: what leaves the Application layer is a DTO, never the EF entity.
        Assert.IsType<StationDto>(result.Value);
        Assert.Equal(stationId, result.Value.Id);
        Assert.Equal("INS", result.Value.Code);
        Assert.Equal("အင်းစိန်", result.Value.NameMy);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task Handle_WithUnknownId_ReturnsNotFound()
    {
        await using var provider = BuildProvider();

        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<GetStationHandler>()
            .Handle(new GetStationQuery(Guid.CreateVersion7()), CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("Network.StationNotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Handle_WithThreeStations_ReturnsPagedEnvelope()
    {
        await using var provider = BuildProvider();
        await CreateAsync(provider, "BGO", "Bago", "ပဲခူး");
        await CreateAsync(provider, "INS", "Insein", "အင်းစိန်");
        await CreateAsync(provider, "YGN", "Yangon", "ရန်ကုန်");

        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ListStationsHandler>()
            .Handle(new ListStationsQuery(Page: 1, PageSize: 50), CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(50, result.Value.PageSize);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Equal(["BGO", "INS", "YGN"], result.Value.Items.Select(station => station.Code));
    }

    /// <summary>
    /// Paging is ordered by code, so walking the pages sees each row exactly once. Without a
    /// deterministic order SQL Server may return rows differently between pages and a caller
    /// would silently see duplicates and omissions.
    /// </summary>
    [Fact]
    public async Task Handle_AcrossPages_ReturnsEachStationExactlyOnce()
    {
        await using var provider = BuildProvider();
        foreach (var code in (string[])["AAA", "BBB", "CCC", "DDD", "EEE"])
        {
            await CreateAsync(provider, code, $"Station {code}", "မြန်မာ");
        }

        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ListStationsHandler>();

        var first = await handler.Handle(new ListStationsQuery(1, 2), CancellationToken);
        var second = await handler.Handle(new ListStationsQuery(2, 2), CancellationToken);
        var third = await handler.Handle(new ListStationsQuery(3, 2), CancellationToken);

        Assert.Equal(["AAA", "BBB"], first.Value.Items.Select(station => station.Code));
        Assert.Equal(["CCC", "DDD"], second.Value.Items.Select(station => station.Code));
        Assert.Equal(["EEE"], third.Value.Items.Select(station => station.Code));
        Assert.All([first, second, third], page => Assert.Equal(5, page.Value.TotalCount));
    }

    [Theory]
    [InlineData(1, 201)]
    [InlineData(1, 0)]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    public async Task Handle_WithPageRequestOutsideLimits_ReturnsValidationError(int page, int pageSize)
    {
        await using var provider = BuildProvider();

        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ListStationsHandler>()
            .Handle(new ListStationsQuery(page, pageSize), CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("Network.InvalidPageRequest", result.Error.Code);
        // ADR-0004 maps Validation to 400, which is what spec S10 asks the API to return.
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task Handle_WithMaximumPageSize_IsAccepted()
    {
        // The boundary itself, so the guard cannot quietly become off-by-one (docs/20 §4).
        await using var provider = BuildProvider();

        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ListStationsHandler>()
            .Handle(new ListStationsQuery(1, Paging.MaxPageSize), CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.Value.PageSize);
    }

    [Fact]
    public async Task Handle_WithNoStations_ReturnsEmptyEnvelope()
    {
        await using var provider = BuildProvider();

        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ListStationsHandler>()
            .Handle(new ListStationsQuery(), CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }

    private async Task<Guid> CreateAsync(IServiceProvider provider, string code, string nameEn, string nameMy)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<CreateStationHandler>()
            .Handle(new CreateStationCommand(code, nameEn, nameMy), CancellationToken);

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
