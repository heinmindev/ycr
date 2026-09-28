using YCR.Domain.Common;

namespace YCR.Domain.Timetable;

/// <summary>
/// A version's creation input after the checks that need no stored data (F-005 plan P3, P4
/// checks 2-5): the name, every time's format, the start date against today, and an empty
/// version's start date.
/// </summary>
/// <remarks>
/// The per-service checks (P4 checks 6-13) need the services' facts, which the handler loads under
/// the Timetable-wide lock, and are decided by <see cref="ScheduleVersion.CreateDraft"/>.
/// </remarks>
public sealed record ScheduleVersionInput
{
    private ScheduleVersionInput(
        BilingualName name,
        DateOnly effectiveFrom,
        IReadOnlyList<ScheduleServiceInput> services)
    {
        Name = name;
        EffectiveFrom = effectiveFrom;
        Services = services;
    }

    public BilingualName Name { get; }

    public DateOnly EffectiveFrom { get; }

    /// <summary>The listed services, in request order.</summary>
    public IReadOnlyList<ScheduleServiceInput> Services { get; }

    /// <summary>
    /// Checks, in this order, the first failure returned (R45; plan P4): (2) the name, R7;
    /// (3) every time's format, in request order — service order, then stop-time order, arrival
    /// before departure — R16; (4) <paramref name="effectiveFrom"/> ≥ <paramref name="today"/>,
    /// R31; (5) a version listing no services starts later than today, R50 (Amendment 2). Check 1,
    /// request shape, runs before this is called.
    /// </summary>
    /// <param name="today">Today's date in the configured local zone (R33).</param>
    /// <remarks>
    /// BUSINESS DECISIONS — provisional tech-lead rulings (hein, 2026-09-26 and 2026-09-27; T-053,
    /// T-054; OQ51, OQ57, OQ60 and Amendment 2) — not Myanma Railways answers. The creation-time
    /// start-date check is an ENGINEERING DECISION (tech lead, hein, 2026-09-26; input consistency).
    /// </remarks>
    public static Result<ScheduleVersionInput> Parse(
        string? nameEn,
        string? nameMy,
        DateOnly effectiveFrom,
        DateOnly today,
        IReadOnlyList<ScheduleServiceText> services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var name = BilingualName.Create(nameEn, nameMy, TimetableErrors.InvalidScheduleVersionName);
        if (name.IsFailure)
        {
            return name.Error;
        }

        var parsed = new List<ScheduleServiceInput>(services.Count);
        foreach (var service in services)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(service.StopTimes);
            var stopTimes = new List<ScheduleStopTimeInput>(service.StopTimes.Count);
            foreach (var stopTime in service.StopTimes)
            {
                ArgumentNullException.ThrowIfNull(stopTime);
                var arrival = ParseOptional(stopTime.Arrival);
                if (arrival.IsFailure)
                {
                    return arrival.Error;
                }

                var departure = ParseOptional(stopTime.Departure);
                if (departure.IsFailure)
                {
                    return departure.Error;
                }

                stopTimes.Add(new ScheduleStopTimeInput(stopTime.Position, arrival.Value.Time, departure.Value.Time));
            }

            parsed.Add(new ScheduleServiceInput(service.ServiceId, stopTimes.AsReadOnly()));
        }

        if (effectiveFrom < today)
        {
            return TimetableErrors.ScheduleVersionEffectiveFromInPast;
        }

        if (parsed.Count == 0 && effectiveFrom <= today)
        {
            return TimetableErrors.EmptyScheduleVersionNotInFuture;
        }

        return new ScheduleVersionInput(name.Value, effectiveFrom, parsed.AsReadOnly());
    }

    /// <summary>A missing time (<c>null</c>) is absent, not invalid; <c>""</c> is invalid (SV11).</summary>
    private static Result<OptionalTime> ParseOptional(string? text)
    {
        if (text is null)
        {
            return new OptionalTime(null);
        }

        var time = TimetableTime.Parse(text);
        return time.IsSuccess ? new OptionalTime(time.Value) : time.Error;
    }

    private sealed record OptionalTime(TimetableTime? Time);
}

/// <summary>One listed service of a create request, as given: times still text.</summary>
public sealed record ScheduleServiceText(Guid ServiceId, IReadOnlyList<ScheduleStopTimeText> StopTimes);

/// <summary>One stop time of a create request, as given: <c>HH:mm</c> text or <c>null</c>.</summary>
public sealed record ScheduleStopTimeText(int Position, string? Arrival, string? Departure);

/// <summary>One listed service after parsing, in request order.</summary>
public sealed record ScheduleServiceInput(Guid ServiceId, IReadOnlyList<ScheduleStopTimeInput> StopTimes);

/// <summary>One stop time after parsing; a <c>null</c> time was not given.</summary>
public sealed record ScheduleStopTimeInput(int Position, TimetableTime? Arrival, TimetableTime? Departure);
