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
/// The entity is materialised and then mapped for the same reason as <c>GetStationHandler</c>:
/// EF cannot translate member access through the <c>StationCode</c> value converter. Only one
/// page is materialised at a time, and <see cref="Paging.MaxPageSize"/> bounds that.
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

        var totalCount = await stations.CountAsync(cancellationToken);
        var page = await stations
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StationDto>(
            [.. page.Select(StationDto.From)],
            query.Page,
            query.PageSize,
            totalCount);
    }
}
