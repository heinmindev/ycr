using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.Abstractions;

/// <summary>
/// The exclusive lock, scoped to one service code, that makes R35 (no overlapping periods for one
/// code) hold under concurrent creates and withdrawals (F-004 plan P9, §R35).
/// </summary>
/// <remarks>
/// Taken inside an explicit transaction on <see cref="ITimetableDbContext.Database"/>, before the
/// reads that decide, and held until that transaction ends (plan P10; ruling Q2, the second use of
/// the F-002 plan P14 exception to ADR-0004's single-save default). Requests on different codes do
/// not wait for each other. It takes a <see cref="ServiceCode"/>, so only a value that passed the
/// code format can name a lock.
/// </remarks>
public interface IServiceCodeLock
{
    /// <exception cref="InvalidOperationException">No transaction is open on the context.</exception>
    Task AcquireAsync(ServiceCode code, CancellationToken cancellationToken);
}
