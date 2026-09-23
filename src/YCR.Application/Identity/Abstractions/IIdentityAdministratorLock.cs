namespace YCR.Application.Identity.Abstractions;

/// <summary>
/// The exclusive lock that makes "at least one active <c>SystemAdministrator</c>" (R27, S19e)
/// hold under concurrent requests (plan P14).
/// </summary>
/// <remarks>
/// Taken inside an explicit transaction on <see cref="IIdentityDbContext.Database"/>, before
/// counting the other active administrators, and held until that transaction ends. This is the
/// one documented exception to ADR-0004's single-save default.
/// </remarks>
public interface IIdentityAdministratorLock
{
    /// <exception cref="InvalidOperationException">No transaction is open on the context.</exception>
    Task AcquireAsync(CancellationToken cancellationToken);
}
