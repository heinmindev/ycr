using Microsoft.EntityFrameworkCore;
using YCR.Application.Timetable.Abstractions;
using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure.Timetable;

/// <summary>
/// F-005 plan P8: the exclusive, transaction-owned application lock <c>timetable.ScheduleVersions</c>
/// (ruling Q5), which serialises create, publish, discard, cancel and service withdrawal (R46).
/// </summary>
/// <remarks>
/// The whole set of versions and the services they list is one resource (ADR-0026 item 2): R19
/// compares one service's period with other rows' dates, which no unique index can enforce
/// (ADR-0026 item 1). <c>sp_getapplock</c> is executable by <c>public</c>, so <c>ycr_app</c> needs
/// no grant (<c>ApplicationCredential_CanTakeTheScheduleVersionsApplock</c>). The resource is a
/// constant but is still passed as a <strong>parameter</strong>, never interpolated into the SQL
/// text. It is a different resource from every <c>timetable.ServiceCode:*</c> lock, so neither
/// blocks the other (plan V6). A negative return code (timeout after 30 s, deadlock victim, error)
/// is raised as a SQL error, so the operation fails instead of running unlocked; it becomes the
/// F-001 opaque <c>500</c> (ADR-0026 item 6).
/// </remarks>
internal sealed class SqlServerScheduleVersionsLock(YcrDbContext context) : IScheduleVersionsLock
{
    public const string Resource = "timetable.ScheduleVersions";

    public async Task AcquireAsync(CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "The schedule-versions lock is transaction-owned; begin a transaction on the Timetable context first.");
        }

        var resource = Resource;
        await context.Database.ExecuteSqlAsync(
            $"""
             DECLARE @result int;
             EXEC @result = sp_getapplock
                 @Resource = {resource},
                 @LockMode = N'Exclusive',
                 @LockOwner = N'Transaction',
                 @LockTimeout = 30000;
             IF @result < 0
                 THROW 50036, N'Could not acquire the schedule-versions lock (R46).', 1;
             """,
            cancellationToken);
    }
}
