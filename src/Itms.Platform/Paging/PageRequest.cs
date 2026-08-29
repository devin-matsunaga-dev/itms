namespace Itms.Platform.Paging;

/// <summary>
/// A page of a list query. Constructed through <see cref="Of"/> so the clamp is
/// applied once, here, instead of being re-implemented (and eventually forgotten) in
/// every list endpoint.
/// </summary>
public readonly record struct PageRequest
{
    /// <summary>The page size used when the caller does not ask for one.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>The largest page the API will serve (ARCHITECTURE.md §6).</summary>
    public const int MaxPageSize = 200;

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>The 1-based page number.</summary>
    public int Page { get; }

    /// <summary>The number of items on the page, never above <see cref="MaxPageSize"/>.</summary>
    public int PageSize { get; }

    /// <summary>The number of rows to skip, for the database query.</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>The number of rows to take, for the database query.</summary>
    public int Take => PageSize;

    /// <summary>
    /// Builds a request from raw query-string values. Out-of-range input is clamped
    /// rather than rejected: a caller asking for page 0 or 10 000 rows wants a page of
    /// results, not a 400, and the clamp is what keeps a hostile page size from
    /// becoming a table scan.
    /// </summary>
    /// <param name="page">The requested 1-based page, or <see langword="null"/> for the first page.</param>
    /// <param name="pageSize">The requested page size, or <see langword="null"/> for <see cref="DefaultPageSize"/>.</param>
    public static PageRequest Of(int? page, int? pageSize) =>
        new(Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
}
