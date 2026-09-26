using Microsoft.EntityFrameworkCore;
using YCR.Application.Timetable.Abstractions;
using YCR.Domain.Timetable;
using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure.Timetable;

/// <summary>
/// F-004 plan P9: an exclusive, transaction-owned application lock named after one service code,
/// which serialises every create and withdrawal of that code (R35).
/// </summary>
/// <remarks>
/// Chosen over an <c>UPDLOCK, HOLDLOCK</c> range read on <c>IX_Services_Code_EffectiveFrom</c>
/// (plan §R35): it names exactly one code, so neighbouring codes never wait; it is taken before any
/// read, so there is no range-lock conversion deadlock; and it does not depend on a query plan.
/// <c>sp_getapplock</c> is executable by <c>public</c>, so <c>ycr_app</c> needs no grant (F-002 V5;
/// <c>ApplicationCredential_CanTakeTheServiceCodeApplock</c>). The resource is passed as a
/// <strong>parameter</strong>, never interpolated into the SQL text (V5), and is built from a
/// <see cref="ServiceCode"/>, which has already passed <c>^[A-Z0-9]{2,10}$</c>. Resource names
/// compare as binary and codes are upper-case by construction, so one code is one resource. A
/// negative return code (timeout after 30 s, deadlock victim, error) is raised as a SQL error, so
/// the operation fails instead of running unlocked; it becomes the F-001 opaque <c>500</c>.
/// </remarks>
internal sealed class SqlServerServiceCodeLock(YcrDbContext context) : IServiceCodeLock
{
    public const string ResourcePrefix = "timetable.ServiceCode:";

    public async Task AcquireAsync(ServiceCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "The service-code lock is transaction-owned; begin a transaction on the Timetable context first.");
        }

        var resource = ResourcePrefix + code.Value;
        await context.Database.ExecuteSqlAsync(
            $"""
             DECLARE @result int;
             EXEC @result = sp_getapplock
                 @Resource = {resource},
                 @LockMode = N'Exclusive',
                 @LockOwner = N'Transaction',
                 @LockTimeout = 30000;
             IF @result < 0
                 THROW 50035, N'Could not acquire the service-code lock (R35).', 1;
             """,
            cancellationToken);
    }
}
