using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Search;
using ProjectLedger.API.Extensions;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

[ApiController]
[Route("api/search")]
[Authorize]
[Tags("Search")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    /// <summary>
    /// Búsqueda global de gastos e ingresos del usuario autenticado.
    /// </summary>
    /// <param name="q">Texto a buscar (mínimo 2 caracteres).</param>
    /// <param name="types">Tipos a buscar: expenses, incomes, o all. Separados por coma. Default: all.</param>
    /// <param name="pageSize">Resultados máximos por tipo. Default: 5.</param>
    [HttpGet]
    [ProducesResponseType(typeof(GlobalSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] string types = "all",
        [FromQuery] int pageSize = 5,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return BadRequest(new { message = "Query must be at least 2 characters." });

        if (pageSize < 1 || pageSize > 50)
            return BadRequest(new { message = "pageSize must be between 1 and 50." });

        var userId = User.GetRequiredUserId();
        var result = await _searchService.SearchAsync(userId, q.Trim(), types, pageSize, ct);
        return Ok(result);
    }
}
