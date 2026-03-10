using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Common;

// ── Request ─────────────────────────────────────────────────

/// <summary>
/// Parámetros de paginación y ordenamiento para endpoints que retornan listas.
/// Se recibe desde [FromQuery] en los controllers.
/// </summary>
public class PagedRequest
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>Número de página (1-based). Por defecto 1.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Cantidad de registros por página. Máximo 100. Por defecto 20.</summary>
    [Range(1, MaxPageSize, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? DefaultPageSize : value;
    }

    /// <summary>Campo por el cual ordenar (depende de cada endpoint). Ejemplo: "createdAt", "title".</summary>
    public string? SortBy { get; set; }

    /// <summary>"asc" o "desc". Por defecto "desc".</summary>
    [RegularExpression("^(asc|desc)$", ErrorMessage = "SortDirection must be 'asc' or 'desc'.")]
    public string SortDirection { get; set; } = "desc";

    // ── Helpers ──────────────────────────────────────────────

    public int Skip => (Page - 1) * PageSize;
    public bool IsDescending => SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
}

// ── Response ────────────────────────────────────────────────

/// <summary>
/// Respuesta paginada genérica. Envuelve cualquier lista de resultados
/// con metadatos de paginación.
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
    /// Crea un PagedResponse a partir de una colección ya paginada + total.
    /// </summary>
    public static PagedResponse<T> Create(IReadOnlyList<T> items, int totalCount, PagedRequest request)
        => new(items, totalCount, request.Page, request.PageSize);
}
