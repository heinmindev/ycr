namespace YCR.Domain.Timetable;

/// <summary>
/// A service listed by a <see cref="ScheduleVersion"/>, with its one set of stop times (F-005
/// spec §7 <c>timetable.ScheduleVersionServices</c>; plan P1).
/// </summary>
/// <remarks>
/// The service is referenced by <see cref="ServiceId"/> only (same module, foreign key, no
/// navigation). One entry per service (R17), with the same times on every operating day (R14).
/// Rows are insert-only (R27, R32).
/// </remarks>
public sealed class ScheduleVersionService
{
    private readonly List<ScheduleStopTime> _stopTimes = [];

    private ScheduleVersionService()
    {
    }

    public Guid ScheduleVersionId { get; private set; }

    public Guid ServiceId { get; private set; }

    /// <summary>The stop times, in position order <c>1..k</c>.</summary>
    public IReadOnlyList<ScheduleStopTime> StopTimes => _stopTimes.AsReadOnly();

    internal static ScheduleVersionService Create(
        Guid scheduleVersionId,
        Guid serviceId,
        IEnumerable<ScheduleStopTimeInput> stopTimesInPositionOrder)
    {
        var entry = new ScheduleVersionService { ScheduleVersionId = scheduleVersionId, ServiceId = serviceId };
        foreach (var stopTime in stopTimesInPositionOrder)
        {
            entry._stopTimes.Add(ScheduleStopTime.Create(
                scheduleVersionId, serviceId, stopTime.Position, stopTime.Arrival, stopTime.Departure));
        }

        return entry;
    }
}
