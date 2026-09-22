namespace YCR.Application.Common.Pagination;

/// <summary>
/// The pagination limits `docs/20` §4 fixes for every collection endpoint.
/// </summary>
/// <remarks>
/// Stated once here rather than repeated per module, so the cap cannot drift between endpoints.
/// The error code a module returns when a request breaks these limits stays with that module,
/// because `docs/20` §2 requires error codes to be `&lt;Module&gt;.&lt;Reason&gt;`.
/// </remarks>
public static class Paging
{
    /// <summary>`docs/20` §4: `?page=1&amp;pageSize=50` (max 200).</summary>
    public const int MaxPageSize = 200;

    public const int DefaultPageSize = 50;

    /// <summary>Whether a request is within the documented limits.</summary>
    public static bool IsWithinLimits(int page, int pageSize) =>
        page >= 1 && pageSize >= 1 && pageSize <= MaxPageSize;
}
