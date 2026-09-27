using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Pagination;
using YCR.Domain.Common;
using YCR.Domain.Timetable;

namespace YCR.Application.Timetable.ListScheduleVersions;

/// <summary>Lists timetable versions in pages, ordered by number (F-005 SV3, SV48; plan P13, P17).</summary>
/// <remarks>
/// <c>Number</c> is unique, so it is a complete order. All paging happens in SQL: a <c>COUNT</c>,
/// then <c>ORDER BY [Number] OFFSET … FETCH NEXT</c>, with <c>serviceCount</c> as a correlated
/// <c>COUNT</c>. Every version is listed, whatever its status (R25), unless a status is given.
/// </remarks>
public sealed class ListScheduleVersionsHandler(ITimetableDbContext db)
{
    public async Task<Result<PagedResult<ScheduleVersionSummaryDto>>> Handle(
        ListScheduleVersionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Paging.IsWithinLimits(query.Page, query.PageSize))
        {
            return TimetableErrors.InvalidPageRequest;
        }

        IQueryable<ScheduleVersion> versions = db.ScheduleVersions.AsNoTracking();
        if (query.Status is not null)
        {
            // The API refuses any other value with 400 Common.ValidationFailed first (Q2).
            var status = ScheduleReadMapping.ParseStatus(query.Status)
                ?? throw new ArgumentException("Status must be exactly Draft, Published, Discarded or Cancelled.", nameof(query));
            versions = versions.Where(version => version.Status == status);
        }

        var ordered = versions.OrderBy(version => version.Number);
        var totalCount = await ordered.CountAsync(cancellationToken);

        var page = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(version => new
            {
                version.Id,
                version.Number,
                version.Name.En,
                version.Name.My,
                version.EffectiveFrom,
                version.Status,
                ServiceCount = version.Services.Count(),
                version.CreatedAtUtc,
                version.PublishedAtUtc,
                version.DiscardedAtUtc,
                version.CancelledAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ScheduleVersionSummaryDto>(
            [.. page.Select(row => new ScheduleVersionSummaryDto(
                row.Id,
                row.Number,
                row.En,
                row.My,
                row.EffectiveFrom,
                ScheduleReadMapping.StatusName(row.Status),
                row.ServiceCount,
                row.CreatedAtUtc,
                row.PublishedAtUtc,
                row.DiscardedAtUtc,
                row.CancelledAtUtc))],
            query.Page,
            query.PageSize,
            totalCount);
    }
}
