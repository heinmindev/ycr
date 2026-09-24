using YCR.Application.Common.Pagination;

namespace YCR.Application.Network.ListRoutes;

/// <summary>Lists routes, one page at a time, inactive ones included (F-003 S3, S13).</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Rows per page, capped at <see cref="Paging.MaxPageSize"/> (docs/20 §4).</param>
public sealed record ListRoutesQuery(int Page = 1, int PageSize = Paging.DefaultPageSize);
