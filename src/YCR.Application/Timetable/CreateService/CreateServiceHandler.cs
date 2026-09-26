using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Abstractions;
using YCR.Application.Network.Contracts;
using YCR.Application.Timetable.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.CreateService;

/// <summary>
/// Creates a service (F-004 S1, S4-S19, S22-S29, S40, S40a, S45, S46).
/// </summary>
/// <remarks>
/// The handler loads facts and maps outcomes; every rule is decided by the domain (plan P4, P13;
/// AGENTS.md rule 3). Order (R37, plan P5): code, names, effective period (<c>400</c>, then the
/// <c>422</c> for an <c>EffectiveTo</c> before today), the route through the Network contract
/// (<c>422</c> when unknown), <see cref="Service.Create"/> (the stop <c>422</c>s), then
/// <strong>begin a transaction, lock the code, load the code's periods, refuse an overlap
/// (<c>409</c>)</strong>, add, audit, one save, commit.
/// <para>
/// R35 cannot be a unique index, so the code lock serialises every create and withdrawal of one
/// code (plan P9, P10; ruling Q2). The Network read happens before the transaction, so the lock is
/// held only for the overlap read and the insert. The route and stop-station active checks are not
/// serialised with deactivation: that race is accepted (spec §5, §0.10 Q3).
/// </para>
/// </remarks>
public sealed class CreateServiceHandler(
    ITimetableDbContext db,
    INetworkReader network,
    IServiceCodeLock codeLock,
    IIdGenerator ids,
    IAuditWriter audit,
    TimeProvider clock,
    ILocalCalendar calendar)
{
    public async Task<Result<Guid>> Handle(CreateServiceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.StopStationIds);
        ArgumentNullException.ThrowIfNull(command.OperatingDays);

        // Shape, already validated by the API (400 Common.ValidationFailed): a bad value here is a
        // caller bug, not a business outcome, so it throws.
        var direction = ParseDirection(command.Direction);
        var operatingDays = OperatingDays.Create([.. command.OperatingDays]);

        var code = ServiceCode.Create(command.Code);
        if (code.IsFailure)
        {
            return code.Error;
        }

        var name = BilingualName.Create(command.NameEn, command.NameMy, TimetableErrors.InvalidServiceName);
        if (name.IsFailure)
        {
            return name.Error;
        }

        var period = EffectivePeriod.ForNewService(command.EffectiveFrom, command.EffectiveTo, calendar.Today());
        if (period.IsFailure)
        {
            return period.Error;
        }

        var route = await network.GetRouteAsync(command.RouteId, cancellationToken);
        if (route is null)
        {
            return TimetableErrors.ServiceRouteNotFound(command.RouteId);
        }

        var created = Service.Create(
            ids.New(),
            code.Value,
            name.Value,
            new ServiceRouteFacts(
                route.Id,
                route.IsActive,
                route.IsClosed,
                [.. route.Stations
                    .OrderBy(station => station.Position)
                    .Select(station => new ServiceRouteStationFacts(station.StationId, station.StationIsActive))]),
            direction,
            command.StopStationIds,
            operatingDays,
            period.Value,
            clock.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error;
        }

        var service = created.Value;

        // Disposing without a commit rolls back and releases the lock.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await codeLock.AcquireAsync(code.Value, cancellationToken);

        // Everything that decides R35 is read after the lock is granted (plan §R35).
        var periods = await db.Services
            .AsNoTracking()
            .Where(other => other.Code == code.Value)
            .Select(other => new { other.EffectiveFrom, other.EffectiveTo })
            .ToListAsync(cancellationToken);
        if (periods.Any(other => period.Value.Overlaps(other.EffectiveFrom, other.EffectiveTo)))
        {
            return TimetableErrors.ServiceCodePeriodOverlap(code.Value.Value);
        }

        db.Services.Add(service);
        audit.Record(
            TimetableAuditActions.ServiceCreated,
            TimetableAuditSubjects.Service,
            service.Id,
            before: null,
            after: ServiceAuditSnapshot.From(
                service,
                route.Code,
                route.Stations.ToDictionary(station => station.StationId, station => station.StationCode)));

        // One save commits the service, its stops and its audit row together (ADR-0004, ADR-0017),
        // so a refused or failed create writes none of them (S46).
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return service.Id;
    }

    /// <summary>Exactly <c>Forward</c> or <c>Reverse</c> (R9; plan P6).</summary>
    private static Direction ParseDirection(string direction) => direction switch
    {
        nameof(Direction.Forward) => Direction.Forward,
        nameof(Direction.Reverse) => Direction.Reverse,
        _ => throw new ArgumentException("Direction must be exactly Forward or Reverse.", nameof(direction))
    };
}
