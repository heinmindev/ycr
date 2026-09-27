using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Abstractions;
using YCR.Application.Timetable.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.DiscardScheduleVersion;

/// <summary>
/// Discards a draft: the row is kept and never applies (plan P7).
/// </summary>
/// <remarks>
/// Order: begin a transaction → take the Timetable-wide lock (R46, with publish on the same draft,
/// SV42) → load the header tracked, without its entries (<c>404</c>) →
/// <see cref="ScheduleVersion.Discard"/> (<c>422</c> not a draft) → audit → one save (one
/// <c>UPDATE</c> of <c>Status</c> and <c>DiscardedAtUtc</c>; <c>409</c> on the token, R47) → commit.
/// </remarks>
public sealed class DiscardScheduleVersionHandler(
    ITimetableDbContext db,
    IScheduleVersionsLock scheduleLock,
    IAuditWriter audit,
    TimeProvider clock)
{
    public async Task<Result> Handle(DiscardScheduleVersionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Disposing without a commit rolls back and releases the lock.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await scheduleLock.AcquireAsync(cancellationToken);

        var version = await db.ScheduleVersions
            .SingleOrDefaultAsync(candidate => candidate.Id == command.ScheduleVersionId, cancellationToken);
        if (version is null)
        {
            return TimetableErrors.ScheduleVersionNotFound;
        }

        var before = ScheduleVersionAuditSnapshot.From(version);
        var discarded = version.Discard(clock.GetUtcNow());
        if (discarded.IsFailure)
        {
            return discarded.Error;
        }

        audit.Record(
            TimetableAuditActions.ScheduleVersionDiscarded,
            TimetableAuditSubjects.ScheduleVersion,
            version.Id,
            before: before,
            after: ScheduleVersionAuditSnapshot.From(version));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // R47: a writer bypassed the lock. Nothing is written; the transaction rolls back.
            return TimetableErrors.ScheduleVersionChangedConcurrently;
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
