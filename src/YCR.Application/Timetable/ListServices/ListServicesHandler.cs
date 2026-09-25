using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Pagination;
using YCR.Application.Network.Contracts;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.ListServices;

/// <summary>Lists services in pages, ordered by code, then effective-from, then id (F-004 S3, S44).</summary>
/// <remarks>
/// Two services may share a code, and even a code and an <c>EffectiveFrom</c> when one never runs
/// (R42), so <c>Id</c> is the final tie-break that keeps pages stable (plan P17). All paging
/// happens in SQL: a <c>COUNT</c>, then <c>ORDER BY … OFFSET … FETCH NEXT</c> with
/// <c>stopCount</c> as a correlated <c>COUNT</c> over <c>ServiceStops</c> (plan V3, asserted by
/// <c>ListServicesSqlTests</c>). Then one Network contract read for the page's route codes.
/// </remarks>
public sealed class ListServicesHandler(ITimetableDbContext db, INetworkReader network)
{
    public async Task<Result<PagedResult<ServiceSummaryDto>>> Handle(
        ListServicesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Paging.IsWithinLimits(query.Page, query.PageSize))
        {
            return TimetableErrors.InvalidPageRequest;
        }

        IQueryable<Service> services = db.Services.AsNoTracking();
        if (query.RouteId is { } routeId)
        {
            services = services.Where(service => service.RouteId == routeId);
        }

        var ordered = services
            .OrderBy(service => service.Code)
            .ThenBy(service => service.EffectiveFrom)
            .ThenBy(service => service.Id);

        var totalCount = await ordered.CountAsync(cancellationToken);

        var page = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(service => new
            {
                service.Id,
                service.Code,
                service.Name.En,
                service.Name.My,
                service.RouteId,
                service.Direction,
                service.OperatingDays.RunsOnMonday,
                service.OperatingDays.RunsOnTuesday,
                service.OperatingDays.RunsOnWednesday,
                service.OperatingDays.RunsOnThursday,
                service.OperatingDays.RunsOnFriday,
                service.OperatingDays.RunsOnSaturday,
                service.OperatingDays.RunsOnSunday,
                StopCount = service.Stops.Count(),
                service.EffectiveFrom,
                service.EffectiveTo,
                service.CreatedAtUtc,
                service.WithdrawnAtUtc
            })
            .ToListAsync(cancellationToken);

        var routes = page.Count == 0
            ? new Dictionary<Guid, RouteSummaryReference>()
            : await network.GetRouteSummariesAsync([.. page.Select(row => row.RouteId).Distinct()], cancellationToken);

        return new PagedResult<ServiceSummaryDto>(
            [.. page.Select(row => new ServiceSummaryDto(
                row.Id,
                row.Code.Value,
                row.En,
                row.My,
                row.RouteId,
                routes[row.RouteId].Code,
                ServiceReadMapping.DirectionName(row.Direction),
                row.StopCount,
                ServiceReadMapping.DayNames(
                    row.RunsOnMonday, row.RunsOnTuesday, row.RunsOnWednesday, row.RunsOnThursday,
                    row.RunsOnFriday, row.RunsOnSaturday, row.RunsOnSunday),
                row.EffectiveFrom,
                row.EffectiveTo,
                ServiceReadMapping.NeverRuns(row.EffectiveFrom, row.EffectiveTo),
                row.CreatedAtUtc,
                row.WithdrawnAtUtc))],
            query.Page,
            query.PageSize,
            totalCount);
    }
}
