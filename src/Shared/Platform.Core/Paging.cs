namespace Platform.Core;

/// <summary>Standard list query parameters: paging, free-text search and sorting.</summary>
public class PageRequest
{
    private int _page = 1;
    private int _pageSize = 25;

    public int Page { get => _page; set => _page = value < 1 ? 1 : value; }
    public int PageSize { get => _pageSize; set => _pageSize = value is < 1 ? 25 : Math.Min(value, 500); }
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }

    public int Skip => (Page - 1) * PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResult<T> From(IEnumerable<T> all, PageRequest request)
    {
        var list = all as IList<T> ?? all.ToList();
        return new PagedResult<T>(list.Skip(request.Skip).Take(request.PageSize).ToList(), list.Count, request.Page, request.PageSize);
    }
}
