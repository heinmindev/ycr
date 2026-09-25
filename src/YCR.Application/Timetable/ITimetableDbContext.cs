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
/// </remarks>
public interface ITimetableDbContext
{
    DbSet<Service> Services { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
