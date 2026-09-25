using Microsoft.EntityFrameworkCore;
using YCR.Application.Common;
using YCR.Application.Common.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Application.Network.CreateRoute;

/// <summary>
/// Creates a route (F-003 S1, S6-S11, S21, S24, S29, S30).
/// </summary>
/// <remarks>
/// The handler loads facts and maps outcomes; every sequence rule is decided by
/// <see cref="Route.Create"/> (plan P4, AGENTS.md rule 3). Order (plan P5): the code and the names
/// are judged first, then the stations are loaded and the route is built (the four <c>422</c>s),
/// then the code is checked against other routes (<c>409</c>), then one save. A request is judged
/// valid in itself before it is judged against other routes, so the <c>409</c> is always last,
/// whether it comes from the pre-check or from the index.
/// <para>
/// <c>UX_Routes_Code</c> is the authority for duplicate codes: between the pre-check and the save a
/// concurrent request can take the code, and only the database can settle that race (S21). The
/// catch matches named constraints only; any other violation propagates, as in
/// <c>CreateStationHandler</c>.
/// </para>
/// <para>
/// The station lookup is not serialised with station deactivation. That race is accepted (spec §5,
/// hein, 2026-09-24): its end state equals "create, then deactivate", which R12 allows.
/// </para>
/// </remarks>
public sealed class CreateRouteHandler(
    INetworkDbContext db,
    IIdGenerator ids,
    IAuditWriter audit,
    TimeProvider clock)
{
    public async Task<Result<Guid>> Handle(CreateRouteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.StationIds);

        var code = RouteCode.Create(command.Code);
        if (code.IsFailure)
        {
            return code.Error;
        }

        var name = BilingualName.Create(command.NameEn, command.NameMy, NetworkErrors.InvalidRouteName);
        if (name.IsFailure)
        {
            return name.Error;
        }

        // One query for every requested id (a single JSON parameter, plan O5). Ids that do not
        // exist are simply absent from the result, which is what Route.Create reads as R16.
        var requested = command.StationIds.Distinct().ToList();
        var stations = await db.Stations
            .AsNoTracking()
            .Where(station => requested.Contains(station.Id))
            .Select(station => new { station.Id, station.Code, station.IsActive })
            .ToListAsync(cancellationToken);

        var created = Route.Create(
            ids.New(),
            code.Value,
            name.Value,
            command.IsClosed,
            command.StationIds,
            stations.ToDictionary(station => station.Id, station => station.IsActive),
            clock.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error;
        }

        // Deactivated routes keep their rows, so this also enforces "never reused" (R14, S30).
        if (await db.Routes.AnyAsync(route => route.Code == code.Value, cancellationToken))
        {
            return NetworkErrors.RouteCodeAlreadyExists(command.Code);
        }

        var route = created.Value;
        db.Routes.Add(route);

        audit.Record(
            "Network.RouteCreated",
            NetworkAuditSubjects.Route,
            route.Id,
            before: null,
            after: RouteAuditSnapshot.From(
                route,
                stations.ToDictionary(station => station.Id, station => station.Code.Value)));

        try
        {
            // One save commits the route, its sequence and its audit row together (ADR-0004,
            // ADR-0017), so a failed create writes none of them (S24).
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException violation)
            when (violation.ConstraintName == NetworkConstraints.RouteCodeUniqueIndex)
        {
            return NetworkErrors.RouteCodeAlreadyExists(command.Code);
        }
        catch (UniqueConstraintViolationException violation)
            when (violation.ConstraintName == NetworkConstraints.RouteStationUniqueIndex)
        {
            // Defence in depth (R7; review C-1, kept as ruled by hein, 2026-09-25). Unreachable
            // while Route.Create refuses every repeated station before the save, so no test drives
            // this branch and none gets a seam to do so. It stays because the index, not the
            // aggregate, is the integrity authority. The translation it depends on (named
            // SQL Server unique violation → UniqueConstraintViolationException.ConstraintName) is
            // covered by UniqueConstraintTranslationTests, and RouteModelTests pins the index name.
            // The error code stays the one the pre-check gives.
            return NetworkErrors.RouteStationRepeated(FirstRepeatedOrFirst(command.StationIds));
        }

        return route.Id;
    }

    private static Guid FirstRepeatedOrFirst(IReadOnlyList<Guid> stationIds)
    {
        var seen = new HashSet<Guid>();
        return stationIds.FirstOrDefault(stationId => !seen.Add(stationId), stationIds[0]);
    }
}
