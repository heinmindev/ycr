using YCR.Application.Common.Pagination;

namespace YCR.Application.Timetable.ListServices;

/// <summary>Lists services, one page at a time, withdrawn ones included (F-004 S3, S44).</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Rows per page, capped at <see cref="Paging.MaxPageSize"/> (docs/20 §4).</param>
/// <param name="RouteId">Only this route's services; an unknown id gives an empty page.</param>
public sealed record ListServicesQuery(int Page = 1, int PageSize = Paging.DefaultPageSize, Guid? RouteId = null);
