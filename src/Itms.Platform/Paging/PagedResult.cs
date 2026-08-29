namespace Itms.Platform.Paging;

/// <summary>
/// The list envelope every paged endpoint returns, fixed by ARCHITECTURE.md §6 as
/// <c>{ items, total, page, pageSize }</c>. It is a type rather than an anonymous
/// object so the shape reaches OpenAPI and, from there, the generated client.
/// </summary>
/// <typeparam name="T">The item DTO. Always a projection — never a loaded aggregate (CONVENTIONS.md).</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="Total">The total number of matching items across all pages.</param>
/// <param name="Page">The 1-based page number this envelope represents.</param>
/// <param name="PageSize">The page size that was applied, after clamping.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    /// <summary>The number of pages the current page size yields for <see cref="Total"/> items.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    /// <summary>True when a further page exists.</summary>
    public bool HasNextPage => Page < TotalPages;
}

/// <summary>
/// Builds <see cref="PagedResult{T}"/> envelopes. A companion type rather than static
/// members on the generic one, so the item type is inferred at the call site.
/// </summary>
public static class PagedResult
{
    /// <summary>Builds an envelope from a materialised page and the total count.</summary>
    public static PagedResult<T> From<T>(IReadOnlyList<T> items, int total, PageRequest request) =>
        new(items, total, request.Page, request.PageSize);

    /// <summary>An empty page, for the "no matches" branch of a list query.</summary>
    public static PagedResult<T> Empty<T>(PageRequest request) =>
        new([], 0, request.Page, request.PageSize);
}
