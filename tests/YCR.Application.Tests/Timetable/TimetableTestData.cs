using YCR.Application.Timetable.CreateService;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// Spec §4's network: RC closed <c>[A, B, C, D, E]</c>, RO open <c>[P, Q, R, S]</c>, and two
/// stations on no route, <c>X</c> and <c>Y</c>; <c>Z</c> is an id that names no station at all.
/// </summary>
public sealed record TimetableNetwork(Guid Rc, Guid Ro, IReadOnlyDictionary<char, Guid> Stations)
{
    public Guid this[char letter] => Stations[letter];

    public List<Guid> Stops(string letters) => [.. letters.Select(letter => Stations[letter])];
}

/// <summary>Spec §4's default create, with named overrides.</summary>
public static class TimetableTestData
{
    public static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>
    /// "A create uses a valid code, names, Monday–Friday, <c>effectiveFrom = 2026-10-05</c> and
    /// <c>effectiveTo = null</c>" (spec §4), on RC, <c>Forward</c>, stopping at A, C and E.
    /// </summary>
    public static CreateServiceCommand Command(
        TimetableNetwork network,
        string stops = "ACE",
        string code = "S101",
        Guid? routeId = null,
        string direction = "Forward",
        string effectiveFrom = "2026-10-05",
        string? effectiveTo = null,
        IReadOnlyList<DayOfWeek>? days = null,
        string nameEn = "Circular",
        string nameMy = "မြို့ပတ်ရထား") =>
        new(
            code,
            nameEn,
            nameMy,
            routeId ?? network.Rc,
            direction,
            network.Stops(stops),
            days ?? Weekdays,
            Date(effectiveFrom),
            effectiveTo is null ? null : Date(effectiveTo));

    public static DateOnly Date(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd");
}
