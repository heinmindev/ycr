using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Abstractions;
using YCR.Application.Timetable.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.CreateScheduleVersion;

/// <summary>
/// Creates a whole draft timetable version (F-005 SV1, SV4-SV16, SV18, SV19, SV22, SV50, SV55;
/// R7-R17, R27, R31, R45, R50; plan P3-P6).
/// </summary>
/// <remarks>
/// The handler loads facts and maps outcomes; every rule is decided by the domain (AGENTS.md rule
/// 3). Order (plan P6): <see cref="ScheduleVersionInput.Parse"/> (checks 2-5, no stored data) →
/// <strong>begin a transaction → take the Timetable-wide lock</strong> → load the requested
/// services' facts (one query) → read the highest number → <see cref="ScheduleVersion.CreateDraft"/>
/// (checks 6-13) → add, audit, one save → commit. The R17 read is under the lock, so it is
/// serialised with withdrawals (R46); the number is the highest ever assigned + 1, so numbers are
/// contiguous and never reused (R7). <c>UX_ScheduleVersions_Number</c> is the backstop: a violation
/// (only from a writer that bypasses the lock) is not caught, so it is the opaque <c>500</c> and
/// nothing is written (P5, SV50).
/// </remarks>
public sealed class CreateScheduleVersionHandler(
    ITimetableDbContext db,
    IScheduleVersionsLock scheduleLock,
    IIdGenerator ids,
    IAuditWriter audit,
    TimeProvider clock,
    ILocalCalendar calendar)
{
    public async Task<Result<CreatedScheduleVersion>> Handle(CreateScheduleVersionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Services);

        var input = ScheduleVersionInput.Parse(
            command.NameEn,
            command.NameMy,
            command.EffectiveFrom,
            calendar.Today(),
            [.. command.Services.Select(service => new ScheduleServiceText(
                service.ServiceId,
                [.. service.StopTimes.Select(stop => new ScheduleStopTimeText(stop.Position, stop.Arrival, stop.Departure))]))]);
        if (input.IsFailure)
        {
            return input.Error;
        }

        // Disposing without a commit rolls back and releases the lock.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await scheduleLock.AcquireAsync(cancellationToken);

        // Everything that decides is read after the lock is granted (R46).
        var facts = await ScheduleServiceFactsReader.LoadAsync(
            db, [.. input.Value.Services.Select(service => service.ServiceId).Distinct()], cancellationToken);
        var highest = await db.ScheduleVersions
            .AsNoTracking()
            .OrderByDescending(version => version.Number)
            .Select(version => (int?)version.Number)
            .FirstOrDefaultAsync(cancellationToken);

        var created = ScheduleVersion.CreateDraft(
            ids.New(),
            ScheduleVersionNumbering.Next(highest),
            input.Value,
            facts,
            clock.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error;
        }

        var version = created.Value;
        db.ScheduleVersions.Add(version);
        audit.Record(
            TimetableAuditActions.ScheduleVersionCreated,
            TimetableAuditSubjects.ScheduleVersion,
            version.Id,
            before: null,
            after: ScheduleVersionCreatedAuditSnapshot.From(
                version,
                facts.ToDictionary(fact => fact.ServiceId, fact => fact.Code)));

        // One save commits the version, its entries, its times and its audit row together
        // (ADR-0004, ADR-0017), so a refused or failed create writes none of them (SV50).
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreatedScheduleVersion(version.Id, version.Number);
    }
}
