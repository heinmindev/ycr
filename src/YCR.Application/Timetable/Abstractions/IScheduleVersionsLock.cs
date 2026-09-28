namespace YCR.Application.Timetable.Abstractions;

/// <summary>
/// The one Timetable-wide, exclusive lock that serialises every operation deciding or changing
/// which versions apply or which services they cover: create (number assignment), publish,
/// discard, cancel and service withdrawal (F-005 spec R46; plan P8).
/// </summary>
/// <remarks>
/// ENGINEERING DECISION (tech lead, hein, 2026-09-26; E8 not accepted → lock; ADR-0026). Taken
/// inside an explicit transaction on <see cref="ITimetableDbContext.Database"/>, before the reads
/// that decide, and held until that transaction ends (plan P24). A handler that also takes the
/// service-code lock (<see cref="IServiceCodeLock"/>) takes that one first; no holder of this lock
/// ever requests a service-code lock, so the two cannot deadlock (plan P9, §The Timetable-wide
/// lock). Reads and service creation do not take it.
/// </remarks>
public interface IScheduleVersionsLock
{
    /// <exception cref="InvalidOperationException">No transaction is open on the context.</exception>
    Task AcquireAsync(CancellationToken cancellationToken);
}
