namespace YCR.Application.Timetable;

/// <summary>The audit actions the Timetable module writes (<c>docs/20</c> §2; F-004 spec §8).</summary>
public static class TimetableAuditActions
{
    public const string ServiceCreated = "Timetable.ServiceCreated";

    public const string ServiceWithdrawn = "Timetable.ServiceWithdrawn";

    /// <summary>F-005 spec §8: a draft was created (full snapshot).</summary>
    public const string ScheduleVersionCreated = "Timetable.ScheduleVersionCreated";

    public const string ScheduleVersionPublished = "Timetable.ScheduleVersionPublished";

    public const string ScheduleVersionDiscarded = "Timetable.ScheduleVersionDiscarded";

    public const string ScheduleVersionCancelled = "Timetable.ScheduleVersionCancelled";
}
