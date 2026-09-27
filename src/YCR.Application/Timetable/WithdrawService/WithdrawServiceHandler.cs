using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Abstractions;
using YCR.Application.Network.Contracts;
using YCR.Application.Timetable.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.WithdrawService;

/// <summary>
/// Withdraws a service from a date (F-004 S26, S29-S39; R21, R35, R36; plan P12).
/// </summary>
/// <remarks>
/// Order: read the service's code by id without the lock (unknown → <c>404</c>); the code never
/// changes (R20), so this read is safe. Then begin a transaction, lock the code, and
/// <strong>reload the service tracked, with its stops</strong>: everything that decides is read
/// after the lock, so two withdrawals of one service run one after the other and the second sees
/// the first's <c>EffectiveTo</c> (S37). <see cref="Service.Withdraw"/> decides R21; one save
/// writes the two columns and the audit row; commit.
/// <para>
/// <c>EffectiveTo</c> is the EF concurrency token, so the save is one
/// <c>UPDATE [timetable].[Services] SET [EffectiveTo], [WithdrawnAtUtc] … WHERE [Id] AND [EffectiveTo]</c>
/// (plan V2) — exactly the two columns <c>ycr_app</c> may update. The stops are loaded for the
/// snapshot and left unchanged. A writer that bypasses the lock makes the <c>WHERE</c> match no row:
/// <see cref="DbUpdateConcurrencyException"/> → <c>409 Timetable.ServiceChangedConcurrently</c>, and
/// the audit row rolls back with it (R36). There is no route or station guard (R21, S39).
/// </para>
/// <para>
/// <strong>F-005 (spec R19, §0.12; plan P9, P10):</strong> after the reload and the Network reads,
/// the handler also takes the Timetable-wide lock <c>timetable.ScheduleVersions</c> — always
/// <em>after</em> the service-code lock; no holder of the Timetable-wide lock ever requests a
/// code lock, so the two cannot deadlock — then reads the published versions that list the service
/// and passes that coverage to <see cref="Service.Withdraw"/>, which refuses with
/// <c>422 Timetable.ServiceInPublishedScheduleVersion</c> after F-004's own checks. The Network
/// reads stay where F-004 put them (outside this feature's scope), so the Timetable-wide lock is
/// held only for the coverage read and the save.
/// </para>
/// </remarks>
public sealed class WithdrawServiceHandler(
    ITimetableDbContext db,
    INetworkReader network,
    IServiceCodeLock codeLock,
    IScheduleVersionsLock scheduleLock,
    IAuditWriter audit,
    TimeProvider clock,
    ILocalCalendar calendar)
{
    public async Task<Result> Handle(WithdrawServiceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var code = await db.Services
            .AsNoTracking()
            .Where(candidate => candidate.Id == command.ServiceId)
            .Select(candidate => candidate.Code)
            .FirstOrDefaultAsync(cancellationToken);
        if (code is null)
        {
            return TimetableErrors.ServiceNotFound;
        }

        // Disposing without a commit rolls back and releases the lock.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await codeLock.AcquireAsync(code, cancellationToken);

        var service = await db.Services
            .Include(candidate => candidate.Stops)
            .SingleAsync(candidate => candidate.Id == command.ServiceId, cancellationToken);

        var routes = await network.GetRouteSummariesAsync([service.RouteId], cancellationToken);
        var stations = await network.GetStationsAsync(
            [.. service.Stops.Select(stop => stop.StationId).Distinct()], cancellationToken);
        var routeCode = routes[service.RouteId].Code;
        var stationCodes = stations.ToDictionary(station => station.Key, station => station.Value.Code);

        var before = ServiceAuditSnapshot.From(service, routeCode, stationCodes);

        // F-005 plan P9: the Timetable-wide lock, after the code lock, before the coverage read.
        await scheduleLock.AcquireAsync(cancellationToken);
        var coverage = await PublishedTimelineReader.LoadCoverageAsync(db, service.Id, cancellationToken);

        var withdrawal = service.Withdraw(command.WithdrawFrom, calendar.Today(), clock.GetUtcNow(), coverage);
        if (withdrawal.IsFailure)
        {
            return withdrawal.Error;
        }

        audit.Record(
            TimetableAuditActions.ServiceWithdrawn,
            TimetableAuditSubjects.Service,
            service.Id,
            before: before,
            after: ServiceAuditSnapshot.From(service, routeCode, stationCodes));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // R36: a writer bypassed the lock. Nothing is written; the transaction rolls back.
            return TimetableErrors.ServiceChangedConcurrently;
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
