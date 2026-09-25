namespace YCR.Application.Network.Contracts;

/// <summary>
/// The Network module's read-only contract for other modules (ADR-0025; F-004 plan P14).
/// </summary>
/// <remarks>
/// ENGINEERING DECISION (tech lead, hein, 2026-09-25; ADR-0025, Accepted): another module reads
/// Network data only through this interface, which returns primitive records (item 1), never a
/// Network domain type, <c>INetworkDbContext</c> or an <c>IQueryable</c>;
/// <c>ContractsMustNotDependOnModuleDomainOrContext</c> enforces it (item 4). It has no write
/// method (item 3). Every value is the row's <em>current</em> value. The implementation runs on the
/// caller's scoped context, so it shares the caller's connection and any open transaction (item 2).
/// </remarks>
public interface INetworkReader
{
    /// <summary>The route with its stations in position order <c>1..n</c>; null when no route has this id.</summary>
    Task<RouteReference?> GetRouteAsync(Guid routeId, CancellationToken cancellationToken);

    /// <summary>Header data for each existing route id; unknown ids are absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, RouteSummaryReference>> GetRouteSummariesAsync(
        IReadOnlyCollection<Guid> routeIds, CancellationToken cancellationToken);

    /// <summary>Each existing station id; unknown ids are absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, StationReference>> GetStationsAsync(
        IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken);
}
