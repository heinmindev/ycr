using Microsoft.EntityFrameworkCore;
using YCR.Application.Identity.Abstractions;
using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure.Identity;

/// <summary>
/// Plan P14: one named, exclusive, transaction-owned application lock serialises every operation
/// that could reduce the set of active <c>SystemAdministrator</c>s (R27, S19e).
/// </summary>
/// <remarks>
/// <c>sp_getapplock</c> is executable by <c>public</c>, so <c>ycr_app</c> needs no grant (V5,
/// tested). A negative return code (timeout, deadlock victim, error) is raised as a SQL error, so
/// the operation fails instead of running unlocked.
/// </remarks>
internal sealed class SqlServerIdentityAdministratorLock(YcrDbContext context) : IIdentityAdministratorLock
{
    public const string Resource = "identity.SystemAdministrators";

    public async Task AcquireAsync(CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "The administrator lock is transaction-owned; begin a transaction on the Identity context first.");
        }

        await context.Database.ExecuteSqlRawAsync(AcquireSql, cancellationToken);
    }

    private const string AcquireSql =
        $"""
         DECLARE @result int;
         EXEC @result = sp_getapplock
             @Resource = N'{Resource}',
             @LockMode = N'Exclusive',
             @LockOwner = N'Transaction',
             @LockTimeout = 30000;
         IF @result < 0
             THROW 50027, N'Could not acquire the SystemAdministrator lock (R27).', 1;
         """;
}
