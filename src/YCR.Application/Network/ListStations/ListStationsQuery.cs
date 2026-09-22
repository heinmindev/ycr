using YCR.Application.Common.Pagination;

namespace YCR.Application.Network.ListStations;

/// <summary>Lists stations, one page at a time (spec S3, S10).</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Rows per page, capped at <see cref="Paging.MaxPageSize"/> (docs/20 §4).</param>
public sealed record ListStationsQuery(int Page = 1, int PageSize = Paging.DefaultPageSize);
