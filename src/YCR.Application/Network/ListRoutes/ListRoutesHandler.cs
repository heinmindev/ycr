using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Pagination;
using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Application.Network.ListRoutes;

/// <summary>Lists routes in pages, ordered by code (F-003 S3, S13).</summary>
/// <remarks>
/// <c>Code</c> is unique, so it is a total order and pages are stable. All paging happens in SQL:
/// a <c>COUNT</c>, then <c>ORDER BY ... OFFSET ... FETCH NEXT</c>, with <c>stationCount</c> as a
/// correlated <c>COUNT</c> over <c>RouteStations</c>, so no sequence row crosses the wire
/// (plan-V3, asserted by <c>ListRoutesSqlTests</c>). The projection takes the whole
/// <see cref="RouteCode"/>, as <c>StationProjection</c> explains.
/// </remarks>
public sealed class ListRoutesHandler(INetworkDbContext db)
{
    public async Task<Result<PagedResult<RouteSummaryDto>>> Handle(
        ListRoutesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Paging.IsWithinLimits(query.Page, query.PageSize))
        {
            return NetworkErrors.InvalidPageRequest;
        }

        var routes = db.Routes.AsNoTracking().OrderBy(route => route.Code);

        var totalCount = await routes.CountAsync(cancellationToken);

        var page = await routes
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(route => new
            {
                route.Id,
                route.Code,
                route.Name.En,
                route.Name.My,
                route.IsClosed,
                route.IsActive,
                StationCount = route.Stations.Count(),
                route.CreatedAtUtc,
                route.DeactivatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<RouteSummaryDto>(
            [.. page.Select(row => new RouteSummaryDto(
                row.Id,
                row.Code.Value,
                row.En,
                row.My,
                row.IsClosed,
                row.IsActive,
                row.StationCount,
                row.CreatedAtUtc,
                row.DeactivatedAtUtc))],
            query.Page,
            query.PageSize,
            totalCount);
    }
}
