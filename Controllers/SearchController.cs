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
    /// Global search of expenses and incomes of the authenticated user.
    /// </summary>
    /// <param name="q">Text to search (minimum 2 characters).</param>
    /// <param name="types">Types to search: expenses, incomes, or all. Comma separated. Default: all.</param>
    /// <param name="pageSize">Maximum results per type. Default: 5.</param>
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
