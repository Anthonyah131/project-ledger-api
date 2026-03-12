using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.DTOs.Mcp;

public class McpContextResponse
{
    public Guid UserId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public string DefaultCurrencyCode { get; set; } = "USD";
    public List<McpVisibleProjectResponse> VisibleProjects { get; set; } = [];
    public Dictionary<string, bool> Permissions { get; set; } = new();
    public Dictionary<string, int?> Limits { get; set; } = new();
}

public class McpVisibleProjectResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string UserRole { get; set; } = null!;
}

public class McpPagedResponse<T> : PagedResponse<T>
{
    public string? SearchNote { get; set; }

    public McpPagedResponse()
    {
    }

    public McpPagedResponse(IReadOnlyList<T> items, int totalCount, int page, int pageSize, string? searchNote = null)
        : base(items, totalCount, page, pageSize)
    {
        SearchNote = searchNote;
    }

    public static McpPagedResponse<T> Create(
        IReadOnlyList<T> items,
        int totalCount,
        PagedRequest request,
        string? searchNote = null)
        => new(items, totalCount, request.Page, request.PageSize, searchNote);
}
