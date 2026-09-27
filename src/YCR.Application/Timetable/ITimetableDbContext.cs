using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable;

/// <summary>
/// The Timetable module's view of the shared context (ADR-0012 item 2; F-004 spec §7 schema
/// <c>timetable</c>; plan P1).
/// </summary>
/// <remarks>
/// There is deliberately no <c>DbSet&lt;ServiceStop&gt;</c>: a stop is reached only through its
/// service. <see cref="Database"/> is exposed for the one explicit transaction per create and per
/// withdrawal that holds the service-code lock (plan P9, P10; ruling Q2, the second use of the
/// F-002 plan P14 exception to ADR-0004's single-save default), as <c>IIdentityDbContext</c> does
/// for the administrator lock.
/// <para>
/// F-005 (plan P24; ADR-0026 item 4): the schedule handlers (create, publish, discard, cancel) and
/// the withdrawal use the same explicit transaction to hold the Timetable-wide lock
/// <c>timetable.ScheduleVersions</c> across their deciding reads and their one save. There is no
/// <c>DbSet</c> for a version's entries or stop times: they are reached only through the version.
/// </para>
/// </remarks>
public interface ITimetableDbContext
{
    DbSet<Service> Services { get; }

    /// <summary>Timetable versions (F-005 plan P1).</summary>
    DbSet<ScheduleVersion> ScheduleVersions { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
