using YCR.Application.Common.Pagination;

namespace YCR.Application.Timetable.ListScheduleVersions;

/// <summary>Lists timetable versions, one page at a time, ordered by number (F-005 SV3, SV48).</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Rows per page, capped at <see cref="Paging.MaxPageSize"/> (docs/20 §4).</param>
/// <param name="Status">Only versions with exactly this status name (<c>Draft</c>, <c>Published</c>,
/// <c>Discarded</c>, <c>Cancelled</c>); the API refuses any other value first (plan P17, Q2).</param>
public sealed record ListScheduleVersionsQuery(int Page = 1, int PageSize = Paging.DefaultPageSize, string? Status = null);
