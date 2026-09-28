using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Abstractions;
using YCR.Application.Timetable.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.CancelScheduleVersion;

/// <summary>
/// Cancels a published version whose start date is later than today (plan P7).
/// </summary>
/// <remarks>
/// Order: begin a transaction → take the Timetable-wide lock (R46: a cancel changes which dates the
/// earlier version applies to, so it is serialised with withdrawal, SV41) → load the header
/// tracked, without its entries (<c>404</c>) → <see cref="ScheduleVersion.Cancel"/> (<c>422</c>
/// not published, already effective) → audit → one save (one <c>UPDATE</c> of <c>Status</c> and
/// <c>CancelledAtUtc</c>; <c>409</c> on the token, R47) → commit. A cancel is never refused because
/// a service was withdrawn while this version was in place (R49, Q2 ruled (a)).
/// </remarks>
public sealed class CancelScheduleVersionHandler(
    ITimetableDbContext db,
    IScheduleVersionsLock scheduleLock,
    IAuditWriter audit,
    TimeProvider clock,
    ILocalCalendar calendar)
{
    public async Task<Result> Handle(CancelScheduleVersionCommand command, CancellationToken cancellationToken)
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
        var cancelled = version.Cancel(calendar.Today(), clock.GetUtcNow());
        if (cancelled.IsFailure)
        {
            return cancelled.Error;
        }

        audit.Record(
            TimetableAuditActions.ScheduleVersionCancelled,
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
