namespace Vyshyvanka.Contracts;

/// <summary>
/// Generic paginated response wrapper.
/// </summary>
public record PagedResponse<T>
{
    public List<T> Items { get; init; } = [];
    public int Skip { get; init; }
    public int Take { get; init; }
    public int TotalCount { get; init; }
}
