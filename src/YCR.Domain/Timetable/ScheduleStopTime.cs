namespace YCR.Domain.Timetable;

/// <summary>
/// The times of one stop of a listed service in a <see cref="ScheduleVersion"/> (F-005 spec §7
/// <c>timetable.ScheduleStopTimes</c>; plan P1).
/// </summary>
/// <remarks>
/// The stop is addressed by <c>(ServiceId, Position)</c>, the service's own stop (spec D16), so a
/// station the service passes without stopping can have no time (R13). Stop 1 has a departure
/// only, the last stop an arrival only, every other stop both (R10). Rows are insert-only (R27,
/// R32).
/// </remarks>
public sealed class ScheduleStopTime
{
    private ScheduleStopTime()
    {
    }

    public Guid ScheduleVersionId { get; private set; }

    public Guid ServiceId { get; private set; }

    public int Position { get; private set; }

    public TimetableTime? Arrival { get; private set; }

    public TimetableTime? Departure { get; private set; }

    internal static ScheduleStopTime Create(
        Guid scheduleVersionId,
        Guid serviceId,
        int position,
        TimetableTime? arrival,
        TimetableTime? departure) =>
        new()
        {
            ScheduleVersionId = scheduleVersionId,
            ServiceId = serviceId,
            Position = position,
            Arrival = arrival,
            Departure = departure
        };
}
