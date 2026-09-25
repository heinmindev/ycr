using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Application.Network.DeactivateRoute;

/// <summary>
/// Deactivates a route, keeping the row and its sequence (F-003 S12, S23; plan P10).
/// </summary>
/// <remarks>
/// The F-001 <c>DeactivateStationHandler</c> pattern. <c>Routes.IsActive</c> is the EF concurrency
/// token, so EF emits one
/// <c>UPDATE [network].[Routes] SET [IsActive], [DeactivatedAtUtc] ... WHERE [Id] = @id AND [IsActive] = @original</c>.
/// Those are the only two columns <c>ycr_app</c> may update (R10). The sequence rows are loaded for
/// the audit snapshot and left unchanged, so nothing is written to <c>RouteStations</c>, which is
/// insert-only (plan-V1, asserted by <c>ListRoutesSqlTests</c>).
/// <para>
/// Under two concurrent requests the loser's <c>WHERE</c> matches no row and EF throws
/// <see cref="DbUpdateConcurrencyException"/>, which maps to the same error as finding the route
/// already inactive. The loser's <c>DeactivatedAtUtc</c> and audit row roll back with it, so the
/// winner's timestamp stands and there is exactly one event (R17, S23).
/// </para>
/// </remarks>
public sealed class DeactivateRouteHandler(INetworkDbContext db, IAuditWriter audit, TimeProvider clock)
{
    public async Task<Result> Handle(DeactivateRouteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var route = await db.Routes
            .Include(candidate => candidate.Stations)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.RouteId, cancellationToken);

        if (route is null)
        {
            return NetworkErrors.RouteNotFound;
        }

        var stationIds = route.Stations.Select(station => station.StationId).ToList();
        var stationCodes = await db.Stations
            .AsNoTracking()
            .Where(station => stationIds.Contains(station.Id))
            .Select(station => new { station.Id, station.Code })
            .ToDictionaryAsync(station => station.Id, station => station.Code.Value, cancellationToken);

        var before = RouteAuditSnapshot.From(route, stationCodes);

        var deactivation = route.Deactivate(clock.GetUtcNow());
        if (deactivation.IsFailure)
        {
            return deactivation.Error;
        }

        audit.Record(
            "Network.RouteDeactivated",
            NetworkAuditSubjects.Route,
            route.Id,
            before: before,
            after: RouteAuditSnapshot.From(route, stationCodes));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request deactivated it first. Same outcome, same error.
            return NetworkErrors.RouteAlreadyInactive;
        }

        return Result.Success();
    }
}
