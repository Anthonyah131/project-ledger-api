using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Common;

// ── Request ─────────────────────────────────────────────────

/// <summary>
/// Pagination and sorting parameters for endpoints that return lists.
/// Received from [FromQuery] in the controllers.
/// </summary>
public class PagedRequest
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>Page number (1-based). Defaults to 1.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Number of records per page. Maximum 100. Defaults to 20.</summary>
    [Range(1, MaxPageSize, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? DefaultPageSize : value;
    }

    /// <summary>Field by which to sort (depends on each endpoint). Example: "createdAt", "title".</summary>
    public string? SortBy { get; set; }

    private bool _descending = true;

    /// <summary>"asc" or "desc". Defaults to "desc".</summary>
    [RegularExpression("^(asc|desc)$", ErrorMessage = "Sort direction must be 'asc' or 'desc'.")]
    public string SortDirection
    {
        get => _descending ? "desc" : "asc";
        set => _descending = value.Equals("desc", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Sort direction as boolean. If provided, takes precedence over sortDirection.</summary>
    public bool IsDescending
    {
        get => _descending;
        set => _descending = value;
    }

    // ── Helpers ──────────────────────────────────────────────

    public int Skip => (Page - 1) * PageSize;
}

// ── Response ────────────────────────────────────────────────

/// <summary>
/// Generic paginated response. Wraps any list of results
/// with pagination metadata.
/// </summary>
public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public PagedResponse() { }

    public PagedResponse(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>
    /// Creates a PagedResponse from an already paginated collection + total count.
    /// </summary>
    public static PagedResponse<T> Create(IReadOnlyList<T> items, int totalCount, PagedRequest request)
        => new(items, totalCount, request.Page, request.PageSize);
}

// ── Lookup Request ───────────────────────────────────────────

/// <summary>
/// Lightweight pagination + search request for lookup/picker endpoints.
/// pageSize is capped at 50 (smaller than the standard 100).
/// </summary>
public class LookupRequest
{
    private const int MaxPageSize = 50;
    private const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    [Range(1, MaxPageSize, ErrorMessage = "Page size must be between 1 and 50.")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? DefaultPageSize : value;
    }

    public string? Search { get; set; }

    public int Skip => (Page - 1) * PageSize;
}

/// <summary>
/// Paginated response with financial total of active movements that match the filters.
/// </summary>
public class PagedWithTotalsResponse<T> : PagedResponse<T>
{
    /// <summary>Sum of amounts of active (non-deleted) movements that meet the applied filters.</summary>
    public decimal TotalActiveAmount { get; set; }

    public PagedWithTotalsResponse() { }

    public PagedWithTotalsResponse(IReadOnlyList<T> items, int totalCount, int page, int pageSize, decimal totalActiveAmount)
        : base(items, totalCount, page, pageSize)
    {
        TotalActiveAmount = totalActiveAmount;
    }

    public static PagedWithTotalsResponse<T> Create(IReadOnlyList<T> items, int totalCount, PagedRequest request, decimal totalActiveAmount)
        => new(items, totalCount, request.Page, request.PageSize, totalActiveAmount);
}
