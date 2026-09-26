using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Domain.Tests.Network;

public sealed class StationTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidCodeAndNames_ReturnsActiveStation()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var code = StationCode.Create("YGN").Value;
        var name = BilingualName.Create("Yangon", "Yangon Myanmar", NetworkErrors.InvalidStationName).Value;

        var station = Station.Create(id, code, name, CreatedAtUtc);

        Assert.Equal(id, station.Id);
        Assert.Equal(code, station.Code);
        Assert.Equal(name, station.Name);
        Assert.True(station.IsActive);
        Assert.Equal(CreatedAtUtc, station.CreatedAtUtc);
        Assert.Empty(station.DomainEvents);
    }

    [Fact]
    public void Create_WithNonUtcOffset_ThrowsArgumentException()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var code = StationCode.Create("YGN").Value;
        var name = BilingualName.Create("Yangon", "Yangon Myanmar", NetworkErrors.InvalidStationName).Value;
        var nonUtc = new DateTimeOffset(2026, 9, 20, 6, 30, 0, TimeSpan.FromHours(6.5));

        Assert.Throws<ArgumentException>(() => Station.Create(id, code, name, nonUtc));
    }

    [Fact]
    public void Deactivate_WhenActive_SetsInactiveAndRaisesEvent()
    {
        var station = CreateStation();

        var result = station.Deactivate();

        Assert.True(result.IsSuccess);
        Assert.False(station.IsActive);
        var domainEvent = Assert.IsType<StationDeactivated>(Assert.Single(station.DomainEvents));
        Assert.Equal(station.Id, domainEvent.StationId);
    }

    [Fact]
    public void Deactivate_WhenInactive_ReturnsError()
    {
        var station = CreateStation();
        station.Deactivate();

        var result = station.Deactivate();

        Assert.True(result.IsFailure);
        Assert.Equal(NetworkErrors.StationAlreadyInactive, result.Error);
        Assert.Single(station.DomainEvents);
    }

    private static Station CreateStation()
    {
        return Station.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            StationCode.Create("YGN").Value,
            BilingualName.Create("Yangon", "Yangon Myanmar", NetworkErrors.InvalidStationName).Value,
            CreatedAtUtc);
    }
}
