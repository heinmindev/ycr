using Microsoft.EntityFrameworkCore;
using YCR.Application.Common;
using YCR.Application.Common.Abstractions;
using YCR.Application.Timetable.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.PublishScheduleVersion;

/// <summary>
/// Publishes a draft (plan P7).
/// </summary>
/// <remarks>
/// Order: <strong>begin a transaction → take the Timetable-wide lock</strong> → load the header
/// tracked, <em>without</em> its entries (<c>404</c>) → load the listed services' facts →
/// <see cref="ScheduleVersion.Publish"/> (<c>422</c> not a draft, start before today, an empty
/// version not starting after today, a listed service no longer effective) → audit → one save →
/// commit. R22 is the filtered unique index's, not a read's (ADR-0026 item 1): a second published
/// version with the same start date fails the save, mapped by the index name to
/// <c>409 Timetable.ScheduleVersionEffectiveFromTaken</c>. <c>Status</c> is the concurrency token
/// (R47), so the save is one <c>UPDATE … SET [Status], [PublishedAtUtc] WHERE [Id] AND [Status]</c>
/// (plan V1, V9); a writer that bypassed the lock gives <c>409 Timetable.ScheduleVersionChangedConcurrently</c>.
/// Either way the audit row rolls back with the transaction.
/// </remarks>
public sealed class PublishScheduleVersionHandler(
    ITimetableDbContext db,
    IScheduleVersionsLock scheduleLock,
    IAuditWriter audit,
    TimeProvider clock,
    ILocalCalendar calendar)
{
    public async Task<Result> Handle(PublishScheduleVersionCommand command, CancellationToken cancellationToken)
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

        var listed = await db.ScheduleVersions
            .Where(candidate => candidate.Id == command.ScheduleVersionId)
            .SelectMany(candidate => candidate.Services.Select(entry => entry.ServiceId))
            .ToListAsync(cancellationToken);
        var facts = await ScheduleServiceFactsReader.LoadAsync(db, listed, cancellationToken);

        var before = ScheduleVersionAuditSnapshot.From(version);
        var published = version.Publish(calendar.Today(), facts, clock.GetUtcNow());
        if (published.IsFailure)
        {
            return published.Error;
        }

        audit.Record(
            TimetableAuditActions.ScheduleVersionPublished,
            TimetableAuditSubjects.ScheduleVersion,
            version.Id,
            before: before,
            after: ScheduleVersionAuditSnapshot.From(version));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException violation)
            when (violation.ConstraintName == TimetableConstraints.ScheduleVersionEffectiveFromPublishedUniqueIndex)
        {
            // R22: the index is the authority. Nothing is written; the transaction rolls back.
            return TimetableErrors.ScheduleVersionEffectiveFromTaken;
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
