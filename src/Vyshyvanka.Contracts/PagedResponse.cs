namespace Vyshyvanka.Contracts;

/// <summary>
/// Generic wrapper for a single page of results returned by list endpoints.
/// Combines the current page of items with the total count so clients can
/// render pagination controls without issuing a separate count request.
/// </summary>
/// <typeparam name="T">The type of the items contained in the page.</typeparam>
public record PagedResponse<T>
{
    /// <summary>The items belonging to the current page. Never <c>null</c>; empty when no results match.</summary>
    public List<T> Items { get; init; } = [];

    /// <summary>The number of items skipped before this page (the zero-based offset of the first item).</summary>
    public int Skip { get; init; }

    /// <summary>The maximum number of items requested for this page (the page size).</summary>
    public int Take { get; init; }

    /// <summary>The total number of items matching the query across all pages, ignoring <see cref="Skip"/> and <see cref="Take"/>.</summary>
    public int TotalCount { get; init; }
}
