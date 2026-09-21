using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Pagination;
using YCR.Application.Network.GetStation;
using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Application.Network.ListStations;

/// <summary>Lists stations in pages (spec S3, S10).</summary>
/// <remarks>
/// Ordered by code so paging is stable: without a deterministic order SQL Server may return rows
/// in a different sequence between pages, and a caller walking the pages would silently see
/// duplicates and omissions. <c>Code</c> is unique, so it is a total order on its own.
/// <para>
/// <strong>All of the paging happens in SQL.</strong> The count is a <c>SELECT COUNT(*)</c> and
/// the page is <c>ORDER BY ... OFFSET ... FETCH NEXT</c>, so only the requested rows cross the
/// wire — never the whole table. <c>ListStationsSqlTests</c> asserts this against the generated
/// SQL, because it is the kind of property that silently regresses into a client-side evaluation.
/// </para>
/// <para>
/// The projection takes whole value-object properties; <see cref="StationProjection"/> explains
/// why reaching inside them does not translate.
/// </para>
/// </remarks>
public sealed class ListStationsHandler(INetworkDbContext db)
{
    public async Task<Result<PagedResult<StationDto>>> Handle(
        ListStationsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Paging.IsWithinLimits(query.Page, query.PageSize))
        {
            return NetworkErrors.InvalidPageRequest;
        }

        var stations = db.Stations.AsNoTracking().OrderBy(station => station.Code);

        // A SQL COUNT over the whole table, before any row is fetched.
        var totalCount = await stations.CountAsync(cancellationToken);

        var page = await stations
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(station => new StationProjection(
                station.Id,
                station.Code,
                station.Name.En,
                station.Name.My,
                station.IsActive,
                station.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<StationDto>(
            [.. page.Select(projection => projection.ToDto())],
            query.Page,
            query.PageSize,
            totalCount);
    }
}
