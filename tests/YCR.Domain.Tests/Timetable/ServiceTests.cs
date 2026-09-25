using System.Reflection;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Domain.Tests.Timetable;

/// <summary>
/// The <see cref="Service"/> aggregate (F-004 plan §Domain changes). Routes as in spec §4: RC is
/// closed <c>[A, B, C, D, E]</c> (n = 5), RO is open <c>[P, Q, R, S]</c>. Today is 2026-10-01.
/// </summary>
public sealed class ServiceTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 10, 1, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LaterUtc = new(2026, 10, 2, 4, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 1);
    private static readonly Guid ServiceId = Guid.Parse("5e000000-0000-0000-0000-000000000001");
    private static readonly Guid RcId = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000c1");
    private static readonly Guid RoId = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");
    private static readonly Guid Unknown = Guid.Parse("00000000-0000-0000-0000-0000000000ff");
    private static readonly Guid OffRoute = Guid.Parse("00000000-0000-0000-0000-0000000000ee");

    private static readonly Dictionary<char, Guid> Stations = "ABCDEPQRS".ToDictionary(
        letter => letter,
        letter => Guid.Parse($"00000000-0000-0000-0000-0000000000{(int)letter:x2}"));

    // ----- Happy path and patterns -----

    [Fact]
    public void Create_ForwardOnClosedRoute_HasContiguousPositionsInRequestOrder()
    {
        var result = Create(Rc(), Direction.Forward, "ACE");

        Assert.True(result.IsSuccess);
        var service = result.Value;
        Assert.Equal(ServiceId, service.Id);
        Assert.Equal("S101", service.Code.Value);
        Assert.Equal("Circular", service.Name.En);
        Assert.Equal("Circular Myanmar", service.Name.My);
        Assert.Equal(RcId, service.RouteId);
        Assert.Equal(Direction.Forward, service.Direction);
        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            service.OperatingDays.Days);
        Assert.Equal(new DateOnly(2026, 10, 5), service.EffectiveFrom);
        Assert.Null(service.EffectiveTo);
        Assert.Equal(NowUtc, service.CreatedAtUtc);
        Assert.Null(service.WithdrawnAtUtc);
        Assert.False(service.NeverRuns);
        Assert.Empty(service.DomainEvents);
        Assert.Equal(
            [(1, S('A')), (2, S('C')), (3, S('E'))],
            service.Stops.Select(stop => (stop.Position, stop.StationId)));
        Assert.All(service.Stops, stop => Assert.Equal(ServiceId, stop.ServiceId));
    }

    [Theory]
    [InlineData(Direction.Forward, "DEAB")] // S4
    [InlineData(Direction.Reverse, "BAED")] // S5
    [InlineData(Direction.Forward, "DAC")] // S6
    [InlineData(Direction.Forward, "CB")] // S6
    public void Create_WrapOnClosedRoute_Succeeds(Direction direction, string stops)
    {
        var result = Create(Rc(), direction, stops);

        Assert.True(result.IsSuccess);
        AssertStops(stops, result.Value);
    }

    [Theory]
    [InlineData(Direction.Forward, "CDEABC")]
    [InlineData(Direction.Forward, "CEBC")]
    [InlineData(Direction.Reverse, "CADC")]
    public void Create_FullCircuit_SucceedsWithClosingStopLast(Direction direction, string stops)
    {
        var result = Create(Rc(), direction, stops);

        Assert.True(result.IsSuccess);
        AssertStops(stops, result.Value);
        Assert.Equal(S('C'), result.Value.Stops[^1].StationId);
        Assert.Equal(stops.Length, result.Value.Stops[^1].Position);
    }

    [Fact]
    public void Create_FullCircuitWithTwoDistinctStations_ReturnsTooFewStops()
    {
        AssertFailure(Create(Rc(), Direction.Forward, "CEC"), TimetableErrors.ServiceTooFewStops);
    }

    [Theory]
    [InlineData("CDEABCD", "Timetable.ServiceStopRepeated", 'C')]
    [InlineData("DACE", "Timetable.ServiceStopsOutOfOrder", 'E')]
    public void Create_MoreThanOneCircuit_IsRefused(string stops, string code, char offending)
    {
        var result = Create(Rc(), Direction.Forward, stops);

        AssertFailure(result, code, S(offending));
    }

    [Theory]
    [InlineData(Direction.Forward, "QR")]
    [InlineData(Direction.Reverse, "SRP")]
    public void Create_OpenRoutePartAndReverse_Succeeds(Direction direction, string stops)
    {
        var result = Create(Ro(), direction, stops);

        Assert.True(result.IsSuccess);
        AssertStops(stops, result.Value);
    }

    [Theory]
    [InlineData(Direction.Forward, "RSP", 'P')]
    [InlineData(Direction.Reverse, "QPS", 'S')]
    public void Create_WrapOnOpenRoute_ReturnsOutOfOrder(Direction direction, string stops, char offending)
    {
        AssertFailure(
            Create(Ro(), direction, stops),
            TimetableErrors.ServiceStopsOutOfOrder(S(offending)));
    }

    [Fact]
    public void Create_ClosureOnOpenRoute_ReturnsRepeated()
    {
        AssertFailure(
            Create(Ro(), Direction.Forward, "PQRP"),
            TimetableErrors.ServiceStopRepeated(S('P')));
    }

    [Theory]
    [InlineData("ABBC", 'B')]
    [InlineData("ABAC", 'A')]
    public void Create_RepeatedStop_ReturnsRepeated(string stops, char offending)
    {
        AssertFailure(
            Create(Rc(), Direction.Forward, stops),
            TimetableErrors.ServiceStopRepeated(S(offending)));
    }

    [Theory]
    [InlineData(true, "ACB", 'B')]
    [InlineData(false, "PRQ", 'Q')]
    public void Create_StopsOutOfOrder_ReturnsOutOfOrder(bool closedRoute, string stops, char offending)
    {
        AssertFailure(
            Create(closedRoute ? Rc() : Ro(), Direction.Forward, stops),
            TimetableErrors.ServiceStopsOutOfOrder(S(offending)));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AA")]
    public void Create_WithFewerThanTwoStops_ReturnsTooFewStops(string stops)
    {
        AssertFailure(Create(Rc(), Direction.Forward, stops), TimetableErrors.ServiceTooFewStops);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_StopNotOnRoute_ReturnsStopNotOnRoute(bool stationExistsElsewhere)
    {
        // The domain cannot tell the two apart and must not: both are "not a station of the route".
        var stranger = stationExistsElsewhere ? OffRoute : Unknown;

        var result = Service.Create(
            ServiceId, Code(), Name(), Rc(), Direction.Forward, [S('A'), stranger, S('C')],
            Weekdays(), Period(), NowUtc);

        AssertFailure(result, TimetableErrors.ServiceStopNotOnRoute(stranger));
    }

    [Fact]
    public void Create_OnInactiveRoute_ReturnsRouteInactive()
    {
        AssertFailure(
            Create(Rc(routeIsActive: false), Direction.Forward, "ACE"),
            TimetableErrors.ServiceRouteInactive(RcId));
    }

    [Fact]
    public void Create_WithInactiveStopStation_ReturnsStopStationInactive()
    {
        AssertFailure(
            Create(Rc(inactive: "C"), Direction.Forward, "ACE"),
            TimetableErrors.ServiceStopStationInactive(S('C')));
    }

    [Fact]
    public void Create_PassingInactiveStationWithoutStopping_Succeeds()
    {
        // S18, spec §0.10 reading 2: only stop stations are checked for being active.
        var result = Create(Rc(inactive: "C"), Direction.Forward, "BD");

        Assert.True(result.IsSuccess);
        AssertStops("BD", result.Value);
    }

    // ----- Precedence (R37) -----

    [Theory]
    // Inactive route before a stop off the route.
    [InlineData(false, "", "AX", "Timetable.ServiceRouteInactive", '-')]
    // A stop off the route before a repeat.
    [InlineData(true, "", "AAX", "Timetable.ServiceStopNotOnRoute", 'X')]
    // A repeat before too few stops.
    // ([A, A, A]: the second A repeats; as a closure it would also have too few stations.)
    [InlineData(true, "", "AAA", "Timetable.ServiceStopRepeated", 'A')]
    // Too few stops is judged before order ([C, E, C]: a closure of 2 distinct stations).
    [InlineData(true, "", "CEC", "Timetable.ServiceTooFewStops", '-')]
    // Order before an inactive stop.
    [InlineData(true, "B", "ACB", "Timetable.ServiceStopsOutOfOrder", 'B')]
    public void Create_WithSeveralFailures_ReportsThemInR37Order(
        bool routeIsActive, string inactive, string stops, string code, char offending)
    {
        var result = Create(Rc(routeIsActive, inactive), Direction.Forward, stops);

        AssertFailure(result, code, offending == '-' ? null : S(offending));
    }

    [Theory]
    // Two stops off the route: the first is reported.
    [InlineData("", "AXYC", "Timetable.ServiceStopNotOnRoute", 'X')]
    // Two repeats: the first repeated occurrence is reported.
    [InlineData("", "ABBCC", "Timetable.ServiceStopRepeated", 'B')]
    // Order breaks twice: the stop at which it first breaks.
    [InlineData("", "ADBEC", "Timetable.ServiceStopsOutOfOrder", 'B')]
    // Two inactive stops: the first in stop order (D), not in route order (B).
    [InlineData("DB", "DAB", "Timetable.ServiceStopStationInactive", 'D')]
    public void Create_WithinOneCheck_ReportsTheFirstOffendingStop(
        string inactive, string stops, string code, char offending)
    {
        var result = Create(Rc(inactive: inactive), Direction.Forward, stops);

        AssertFailure(result, code, S(offending));
    }

    // ----- Exhaustive -----

    [Fact]
    public void Create_AnyTwoDistinctStationsOnClosedRoute_SucceedInBothDirections()
    {
        foreach (var direction in Enum.GetValues<Direction>())
        {
            foreach (var first in "ABCDE")
            {
                foreach (var second in "ABCDE".Where(letter => letter != first))
                {
                    var result = Create(Rc(), direction, $"{first}{second}");

                    Assert.True(result.IsSuccess, $"{direction} [{first}, {second}]");
                }
            }
        }
    }

    [Fact]
    public void Create_OnSmallRoutes_AgreesWithWalkingSimulation()
    {
        var checkedLists = 0;
        for (var n = 3; n <= 6; n++)
        {
            var stations = Enumerable.Range(1, n)
                .Select(index => Guid.Parse($"00000000-0000-0000-0001-{index:x12}"))
                .ToList();

            foreach (var isClosed in new[] { true, false })
            {
                var route = new ServiceRouteFacts(
                    RcId,
                    IsActive: true,
                    isClosed,
                    [.. stations.Select(station => new ServiceRouteStationFacts(station, IsActive: true))]);

                foreach (var direction in Enum.GetValues<Direction>())
                {
                    foreach (var positions in AllStopLists(n, maxLength: n + 1))
                    {
                        var expected = WalkIsValid(n, isClosed, direction, positions);

                        var actual = Service.Create(
                            ServiceId, Code(), Name(), route, direction,
                            [.. positions.Select(position => stations[position])],
                            Weekdays(), Period(), NowUtc);

                        Assert.True(
                            expected == actual.IsSuccess,
                            $"n={n} closed={isClosed} {direction} [{string.Join(", ", positions.Select(p => p + 1))}]: " +
                            $"simulation says {(expected ? "valid" : "invalid")}, Service.Create says " +
                            $"{(actual.IsSuccess ? "created" : actual.Error.Code)}");
                        checkedLists++;
                    }
                }
            }
        }

        // 2 x 2 x (sum over n = 3..6 of n^1 + ... + n^(n+1)) stop lists.
        Assert.Equal(4 * (120 + 1364 + 19530 + 335922), checkedLists);
    }

    // ----- Time -----

    [Fact]
    public void Create_WithNonUtcTime_Throws()
    {
        var nonUtc = new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.FromHours(6.5));

        Assert.Throws<ArgumentException>(() => Service.Create(
            ServiceId, Code(), Name(), Rc(), Direction.Forward, Stops("ACE"), Weekdays(), Period(), nonUtc));
    }

    // ----- Withdrawal (R21, R36, R41) -----

    [Fact]
    public void Withdraw_FromFutureDate_SetsEffectiveToDayBeforeAndWithdrawnAt()
    {
        var service = Created();

        var result = service.Withdraw(new DateOnly(2026, 11, 1), Today, LaterUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 10, 31), service.EffectiveTo);
        Assert.Equal(LaterUtc, service.WithdrawnAtUtc);
        Assert.Equal(new DateOnly(2026, 10, 5), service.EffectiveFrom);
        Assert.False(service.NeverRuns);
        Assert.Equal(3, service.Stops.Count);
    }

    [Fact]
    public void Withdraw_FromToday_EndsYesterday()
    {
        var service = Created(from: new DateOnly(2026, 9, 1));

        var result = service.Withdraw(Today, Today, LaterUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 30), service.EffectiveTo);
    }

    [Fact]
    public void Withdraw_FromPastDate_ReturnsWithdrawalDateInPastAndChangesNothing()
    {
        var service = Created();

        var result = service.Withdraw(new DateOnly(2026, 9, 30), Today, LaterUtc);

        AssertFailure(result, TimetableErrors.WithdrawalDateInPast);
        Assert.Null(service.EffectiveTo);
        Assert.Null(service.WithdrawnAtUtc);
    }

    [Theory]
    // S33: Y ends 2026-12-31; a new end of 2027-01-31 would extend it.
    [InlineData("2026-10-05", "2026-12-31", "2026-09-01", "2027-02-01")]
    // S33: a new end of 2026-12-31 would not change it.
    [InlineData("2026-10-05", "2026-12-31", "2026-09-01", "2027-01-01")]
    // S34: Z already ended on 2026-09-30 (created when that was not yet past); from today would reopen it.
    [InlineData("2026-09-01", "2026-09-30", "2026-09-01", "2026-10-01")]
    public void Withdraw_ThatWouldExtendOrKeepTheEnd_ReturnsDoesNotShortenAndChangesNothing(
        string from, string to, string createdOn, string withdrawFrom)
    {
        var service = Created(Date(from), Date(to), today: Date(createdOn));

        var result = service.Withdraw(Date(withdrawFrom), Today, LaterUtc);

        AssertFailure(result, TimetableErrors.WithdrawalDoesNotShorten);
        Assert.Equal(Date(to), service.EffectiveTo);
        Assert.Null(service.WithdrawnAtUtc);
    }

    [Fact]
    public void Withdraw_SecondTime_ShortensFurtherButNeverLengthens()
    {
        var service = Created();
        Assert.True(service.Withdraw(new DateOnly(2026, 11, 1), Today, NowUtc).IsSuccess);

        var shorter = service.Withdraw(new DateOnly(2026, 10, 20), Today, LaterUtc);

        Assert.True(shorter.IsSuccess);
        Assert.Equal(new DateOnly(2026, 10, 19), service.EffectiveTo);
        Assert.Equal(LaterUtc, service.WithdrawnAtUtc);

        var longer = service.Withdraw(new DateOnly(2026, 11, 15), Today, LaterUtc.AddHours(1));

        AssertFailure(longer, TimetableErrors.WithdrawalDoesNotShorten);
        Assert.Equal(new DateOnly(2026, 10, 19), service.EffectiveTo);
        Assert.Equal(LaterUtc, service.WithdrawnAtUtc);
    }

    [Theory]
    [InlineData("2026-10-15")] // S36: before EffectiveFrom
    [InlineData("2026-11-01")] // on EffectiveFrom: it ends the day before it starts
    public void Withdraw_OnOrBeforeEffectiveFrom_NeverRuns(string withdrawFrom)
    {
        var service = Created(from: new DateOnly(2026, 11, 1));

        var result = service.Withdraw(Date(withdrawFrom), Today, LaterUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 11, 1), service.EffectiveFrom);
        Assert.Equal(Date(withdrawFrom).AddDays(-1), service.EffectiveTo);
        Assert.True(service.NeverRuns);
    }

    [Fact]
    public void Withdraw_WithNonUtcTime_Throws()
    {
        var service = Created();
        var nonUtc = new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.FromHours(6.5));

        Assert.Throws<ArgumentException>(() => service.Withdraw(new DateOnly(2026, 11, 1), Today, nonUtc));
        Assert.Null(service.EffectiveTo);
    }

    // ----- Structure (R8, R14, R20, R22, R29) -----

    [Fact]
    public void Service_ExposesNoMutatorOtherThanWithdraw()
    {
        const BindingFlags declaredPublic =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var methods = typeof(Service).GetMethods(declaredPublic)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Order()
            .ToList();
        Assert.Equal(["Create", "Withdraw"], methods);
        Assert.True(typeof(Service).GetMethod(nameof(Service.Create))!.IsStatic);

        Assert.All(
            typeof(Service).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => Assert.True(
                property.SetMethod is null || !property.SetMethod.IsPublic,
                $"{property.Name} has a public setter"));
        Assert.Equal(typeof(IReadOnlyList<ServiceStop>), typeof(Service).GetProperty(nameof(Service.Stops))!.PropertyType);
    }

    [Fact]
    public void ServiceAndServiceStop_HaveNoNavigationToNetworkTypes()
    {
        var types = new[] { typeof(Service), typeof(ServiceStop), typeof(OperatingDays), typeof(ServiceCode) };

        foreach (var type in types)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.DoesNotContain(".Network", property.PropertyType.FullName ?? string.Empty, StringComparison.Ordinal);
                Assert.All(
                    property.PropertyType.GenericTypeArguments,
                    argument => Assert.DoesNotContain(".Network", argument.FullName ?? string.Empty, StringComparison.Ordinal));
            }
        }

        Assert.Equal(typeof(Guid), typeof(Service).GetProperty(nameof(Service.RouteId))!.PropertyType);
    }

    [Fact]
    public void ServiceStop_HasOnlyServiceIdPositionAndStationId()
    {
        var properties = typeof(ServiceStop)
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(property => (property.Name, property.PropertyType))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [("Position", typeof(int)), ("ServiceId", typeof(Guid)), ("StationId", typeof(Guid))],
            properties);
    }

    // ----- Helpers -----

    /// <summary>
    /// The independent oracle (plan §Domain changes): walk the route one station at a time in the
    /// direction, wrapping only on a closed route, until the next listed stop is reached. Valid iff
    /// every stop is reached without visiting a station twice, except arriving back at the first
    /// stop as the last one; and there are at least 2 stops, or 3 distinct stations for a circuit.
    /// </summary>
    private static bool WalkIsValid(int n, bool isClosed, Direction direction, IReadOnlyList<int> stops)
    {
        if (stops.Count == 0)
        {
            return false;
        }

        var visited = new HashSet<int> { stops[0] };
        var current = stops[0];
        for (var i = 1; i < stops.Count; i++)
        {
            var target = stops[i];
            var isClosingStop = i == stops.Count - 1 && target == stops[0];
            while (true)
            {
                var next = direction == Direction.Forward ? current + 1 : current - 1;
                if (next < 0 || next >= n)
                {
                    if (!isClosed)
                    {
                        return false;
                    }

                    next = (next + n) % n;
                }

                current = next;
                if (current == target)
                {
                    if (visited.Contains(current) && !isClosingStop)
                    {
                        return false;
                    }

                    visited.Add(current);
                    break;
                }

                if (!visited.Add(current))
                {
                    return false;
                }
            }
        }

        var closes = stops.Count >= 2 && stops[^1] == stops[0];
        var distinct = stops.Distinct().Count();
        return closes ? distinct >= 3 : stops.Count >= 2;
    }

    private static IEnumerable<IReadOnlyList<int>> AllStopLists(int n, int maxLength)
    {
        for (var length = 1; length <= maxLength; length++)
        {
            var indexes = new int[length];
            while (true)
            {
                yield return (int[])indexes.Clone();

                var digit = length - 1;
                while (digit >= 0 && ++indexes[digit] == n)
                {
                    indexes[digit] = 0;
                    digit--;
                }

                if (digit < 0)
                {
                    break;
                }
            }
        }
    }

    private static Result<Service> Create(ServiceRouteFacts route, Direction direction, string stops) =>
        Service.Create(ServiceId, Code(), Name(), route, direction, Stops(stops), Weekdays(), Period(), NowUtc);

    private static Service Created(DateOnly? from = null, DateOnly? to = null, DateOnly? today = null) =>
        Service.Create(
            ServiceId, Code(), Name(), Rc(), Direction.Forward, Stops("ACE"), Weekdays(),
            EffectivePeriod.ForNewService(from ?? new DateOnly(2026, 10, 5), to, today ?? Today).Value,
            NowUtc).Value;

    private static ServiceRouteFacts Rc(bool routeIsActive = true, string inactive = "") =>
        Route(RcId, isClosed: true, "ABCDE", routeIsActive, inactive);

    private static ServiceRouteFacts Ro() => Route(RoId, isClosed: false, "PQRS", routeIsActive: true, inactive: "");

    private static ServiceRouteFacts Route(Guid id, bool isClosed, string letters, bool routeIsActive, string inactive) =>
        new(id, routeIsActive, isClosed, [.. letters.Select(letter =>
            new ServiceRouteStationFacts(S(letter), !inactive.Contains(letter, StringComparison.Ordinal)))]);

    /// <summary>A letter's station; <c>X</c> and <c>Y</c> name stations on no route.</summary>
    private static Guid S(char letter) => letter switch
    {
        'X' => OffRoute,
        'Y' => Unknown,
        _ => Stations[letter]
    };

    private static List<Guid> Stops(string letters) => [.. letters.Select(S)];

    private static ServiceCode Code() => ServiceCode.Create("S101").Value;

    private static BilingualName Name() =>
        BilingualName.Create("Circular", "Circular Myanmar", TimetableErrors.InvalidServiceName).Value;

    private static OperatingDays Weekdays() => OperatingDays.Create(
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);

    private static EffectivePeriod Period() => EffectivePeriod.ForNewService(new DateOnly(2026, 10, 5), null, Today).Value;

    private static DateOnly Date(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd");

    private static void AssertStops(string letters, Service service) =>
        Assert.Equal(
            letters.Select((letter, index) => (index + 1, S(letter))),
            service.Stops.Select(stop => (stop.Position, stop.StationId)));

    private static void AssertFailure(Result result, Error expected)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(expected, result.Error);
    }

    private static void AssertFailure(Result result, string code, Guid? offending)
    {
        Assert.True(result.IsFailure, "expected a failure");
        Assert.Equal(code, result.Error.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        if (offending is { } stationId)
        {
            Assert.Contains(stationId.ToString(), result.Error.Message, StringComparison.Ordinal);
        }
    }
}
