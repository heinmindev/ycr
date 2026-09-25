using System.Reflection;
using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Domain.Tests.Network;

public sealed class RouteTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DeactivatedAtUtc = new(2026, 9, 25, 3, 0, 0, TimeSpan.Zero);
    private static readonly Guid RouteId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid D = Guid.Parse("00000000-0000-0000-0000-00000000000d");
    private static readonly Guid Unknown = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    [Fact]
    public void Create_WithThreeActiveStations_ReturnsActiveOpenRouteWithPositionsOneToThree()
    {
        var result = Create(isClosed: false, [A, B, C]);

        Assert.True(result.IsSuccess);
        var route = result.Value;
        Assert.Equal(RouteId, route.Id);
        Assert.Equal("LOOP1", route.Code.Value);
        Assert.Equal("Loop", route.Name.En);
        Assert.Equal("Loop Myanmar", route.Name.My);
        Assert.False(route.IsClosed);
        Assert.True(route.IsActive);
        Assert.Equal(CreatedAtUtc, route.CreatedAtUtc);
        Assert.Null(route.DeactivatedAtUtc);
        Assert.Empty(route.DomainEvents);
        Assert.Equal(
            [(1, A), (2, B), (3, C)],
            route.Stations.Select(s => (s.Position, s.StationId)));
        Assert.All(route.Stations, s => Assert.Equal(RouteId, s.RouteId));
    }

    [Fact]
    public void Create_WithNonUtcTime_Throws()
    {
        var nonUtc = new DateTimeOffset(2026, 9, 24, 6, 30, 0, TimeSpan.FromHours(6.5));

        Assert.Throws<ArgumentException>(() => Route.Create(
            RouteId, Code(), Name(), isClosed: false, [A, B], AllActive(), nonUtc));
    }

    [Theory]
    [InlineData(false, new[] { 'A', 'B', 'A' })]
    [InlineData(false, new[] { 'A', 'A' })]
    [InlineData(true, new[] { 'A', 'B', 'C', 'A' })]
    public void Create_WithRepeatedStation_ReturnsRouteStationRepeated(bool isClosed, char[] sequence)
    {
        var result = Create(isClosed, sequence.Select(Station).ToList());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        Assert.Equal("Network.RouteStationRepeated", result.Error.Code);
        Assert.Contains(A.ToString(), result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_OpenWithOneStation_ReturnsRouteTooFewStations()
    {
        var result = Create(isClosed: false, [A]);

        Assert.True(result.IsFailure);
        Assert.Equal(NetworkErrors.RouteTooFewStations, result.Error);
    }

    [Fact]
    public void Create_OpenWithTwoStations_Succeeds()
    {
        var result = Create(isClosed: false, [A, B]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Stations.Count);
    }

    [Fact]
    public void Create_ClosedWithTwoStations_ReturnsRouteTooFewStations()
    {
        var result = Create(isClosed: true, [A, B]);

        Assert.True(result.IsFailure);
        Assert.Equal(NetworkErrors.RouteTooFewStations, result.Error);
    }

    [Fact]
    public void Create_ClosedWithThreeStations_Succeeds()
    {
        var result = Create(isClosed: true, [A, B, C]);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsClosed);
        Assert.Equal(3, result.Value.Stations.Count);
    }

    [Fact]
    public void Create_WithUnknownStation_ReturnsRouteStationNotFound()
    {
        var result = Create(isClosed: false, [A, Unknown, B]);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        Assert.Equal("Network.RouteStationNotFound", result.Error.Code);
        Assert.Contains(Unknown.ToString(), result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WithInactiveStation_ReturnsRouteStationInactive()
    {
        var states = AllActive();
        states[B] = false;

        var result = Route.Create(RouteId, Code(), Name(), isClosed: false, [A, B, C], states, CreatedAtUtc);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        Assert.Equal("Network.RouteStationInactive", result.Error.Code);
        Assert.Contains(B.ToString(), result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WithSeveralFailures_ReportsThemInFixedPrecedence()
    {
        var states = AllActive();
        states[B] = false;
        states[C] = false;

        // R7 before R8: a repeat in a one-distinct-station list is reported as a repeat.
        Assert.Equal(
            "Network.RouteStationRepeated",
            Route.Create(RouteId, Code(), Name(), false, [Unknown, Unknown], states, CreatedAtUtc).Error.Code);

        // R8 before R16 and R11: too short, with an unknown id in it.
        Assert.Equal(
            NetworkErrors.RouteTooFewStations,
            Route.Create(RouteId, Code(), Name(), true, [Unknown, B], states, CreatedAtUtc).Error);

        // R16 before R11, whatever their order in the sequence.
        Assert.Equal(
            NetworkErrors.RouteStationNotFound(Unknown),
            Route.Create(RouteId, Code(), Name(), false, [B, Unknown], states, CreatedAtUtc).Error);

        // Within one rule, the first offending id in sequence order is reported.
        Assert.Equal(
            NetworkErrors.RouteStationInactive(C),
            Route.Create(RouteId, Code(), Name(), false, [A, C, B], states, CreatedAtUtc).Error);
        Assert.Equal(
            NetworkErrors.RouteStationRepeated(B),
            Route.Create(RouteId, Code(), Name(), false, [A, B, C, B, A], AllActive(), CreatedAtUtc).Error);
    }

    [Fact]
    public void Create_ClosedRoute_HasFirstStationOnlyAtPositionOne()
    {
        var route = Create(isClosed: true, [A, B, C]).Value;

        Assert.True(route.IsClosed);
        Assert.Equal(3, route.Stations.Count);
        var first = Assert.Single(route.Stations, s => s.StationId == A);
        Assert.Equal(1, first.Position);
        Assert.Equal(C, route.Stations[^1].StationId);
    }

    [Fact]
    public void Deactivate_WhenActive_SetsInactiveAndDeactivatedAtAndRaisesEvent()
    {
        var route = Create(isClosed: false, [A, B]).Value;

        var result = route.Deactivate(DeactivatedAtUtc);

        Assert.True(result.IsSuccess);
        Assert.False(route.IsActive);
        Assert.Equal(DeactivatedAtUtc, route.DeactivatedAtUtc);
        var domainEvent = Assert.IsType<RouteDeactivated>(Assert.Single(route.DomainEvents));
        Assert.Equal(RouteId, domainEvent.RouteId);
        Assert.Equal([A, B], route.Stations.Select(s => s.StationId));
    }

    [Fact]
    public void Deactivate_WhenInactive_ReturnsRouteAlreadyInactiveAndKeepsFirstTimestamp()
    {
        var route = Create(isClosed: false, [A, B]).Value;
        route.Deactivate(DeactivatedAtUtc);

        var result = route.Deactivate(DeactivatedAtUtc.AddHours(1));

        Assert.True(result.IsFailure);
        Assert.Equal(NetworkErrors.RouteAlreadyInactive, result.Error);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        Assert.Equal(DeactivatedAtUtc, route.DeactivatedAtUtc);
        Assert.Single(route.DomainEvents);
    }

    [Fact]
    public void Deactivate_WithNonUtcTime_Throws()
    {
        var route = Create(isClosed: false, [A, B]).Value;
        var nonUtc = new DateTimeOffset(2026, 9, 25, 9, 30, 0, TimeSpan.FromHours(6.5));

        Assert.Throws<ArgumentException>(() => route.Deactivate(nonUtc));
        Assert.True(route.IsActive);
        Assert.Null(route.DeactivatedAtUtc);
        Assert.Empty(route.DomainEvents);
    }

    [Fact]
    public void Route_ExposesNoMutatorOtherThanDeactivate()
    {
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

        foreach (var type in new[] { typeof(Route), typeof(RouteStation) })
        {
            var publicSetters = type.GetProperties(Public)
                .Where(p => p.SetMethod is { IsPublic: true })
                .Select(p => p.Name);
            Assert.Empty(publicSetters);
        }

        var stateChangingMethods = typeof(Route)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name);
        Assert.Equal(["Deactivate"], stateChangingMethods);

        Assert.DoesNotContain(
            typeof(RouteStation).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            m => !m.IsSpecialName);

        var stations = typeof(Route).GetProperty(nameof(Route.Stations))!;
        Assert.Equal(typeof(IReadOnlyList<RouteStation>), stations.PropertyType);
        var route = Create(isClosed: false, [A, B]).Value;
        Assert.False(route.Stations is IList<RouteStation> { IsReadOnly: false });
    }

    [Fact]
    public void RouteStation_HasNoNavigationToStation()
    {
        var properties = typeof(RouteStation).GetProperties(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.DoesNotContain(properties, p => p.PropertyType == typeof(Station));
        Assert.Equal(
            ["Position", "RouteId", "StationId"],
            properties.Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    private static Result<Route> Create(bool isClosed, IReadOnlyList<Guid> stationIds) =>
        Route.Create(RouteId, Code(), Name(), isClosed, stationIds, AllActive(), CreatedAtUtc);

    private static RouteCode Code() => RouteCode.Create("LOOP1").Value;

    private static BilingualName Name() =>
        BilingualName.Create("Loop", "Loop Myanmar", NetworkErrors.InvalidRouteName).Value;

    private static Dictionary<Guid, bool> AllActive() =>
        new() { [A] = true, [B] = true, [C] = true, [D] = true };

    private static Guid Station(char name) => name switch
    {
        'A' => A,
        'B' => B,
        'C' => C,
        _ => D
    };
}
