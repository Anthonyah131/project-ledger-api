using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Category;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Project categories controller.
/// 
/// Nested route: /api/projects/{projectId}/categories
/// - ProjectId ALWAYS comes from the route, never from the body.
/// - Viewer+ can list/view. Editor+ can create/edit/delete.
/// - Plan validates MaxCategoriesPerProject on creation.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/categories")]
[Authorize]
[Tags("Categories")]
[Produces("application/json")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly IProjectAccessService _accessService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IStringLocalizer<Messages> _localizer;

    public CategoryController(
        ICategoryService categoryService,
        IProjectAccessService accessService,
        IPlanAuthorizationService planAuth,
        IStringLocalizer<Messages> localizer)
    {
        _categoryService = categoryService;
        _accessService = accessService;
        _planAuth = planAuth;
        _localizer = localizer;
    }

    // ── GET /api/projects/{projectId}/categories ────────────

    /// <summary>
    /// Lists all project categories.
    /// </summary>
    /// <response code="200">List of categories.</response>
    /// <response code="403">No access to the project.</response>
    [HttpGet]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken ct)
    {
        var categories = await _categoryService.GetByProjectIdAsync(projectId, ct);
        return Ok(categories.ToResponse());
    }

    // ── GET /api/projects/{projectId}/categories/{categoryId}

    /// <summary>
    /// Gets a category by ID.
    /// </summary>
    /// <response code="200">Category found.</response>
    /// <response code="404">Category not found or does not belong to the project.</response>
    [HttpGet("{categoryId:guid}")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid projectId, Guid categoryId, CancellationToken ct)
    {
        var category = await _categoryService.GetByIdAsync(categoryId, ct);
        if (category is null || category.CatProjectId != projectId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["CategoryNotFound"]));

        return Ok(category.ToResponse());
    }

    // ── POST /api/projects/{projectId}/categories ───────────

    /// <summary>
    /// Creates a category in the project. Requires editor+.
    /// Validates the owner's plan MaxCategoriesPerProject limit.
    /// </summary>
    /// <response code="201">Category created.</response>
    /// <response code="403">No permissions or plan limit exceeded.</response>
    [HttpPost]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateCategoryRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        var category = request.ToEntity(projectId);
        await _categoryService.CreateAsync(category, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { projectId, categoryId = category.CatId },
            category.ToResponse());
    }

    // ── PUT /api/projects/{projectId}/categories/{categoryId}

    /// <summary>
    /// Updates a category. Requires editor+.
    /// </summary>
    /// <response code="200">Category updated.</response>
    /// <response code="404">Category not found.</response>
    [HttpPut("{categoryId:guid}")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid projectId,
        Guid categoryId,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        var category = await _categoryService.GetByIdAsync(categoryId, ct);
        if (category is null || category.CatProjectId != projectId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["CategoryNotFound"]));

        category.ApplyUpdate(request);
        await _categoryService.UpdateAsync(category, userId, ct);

        return Ok(category.ToResponse());
    }

    // ── DELETE /api/projects/{projectId}/categories/{categoryId}

    /// <summary>
    /// Soft-deletes a category. Requires editor+.
    /// </summary>
    /// <response code="400">The category cannot be deleted because it has related active transactions.</response>
    /// <response code="204">Category deleted.</response>
    /// <response code="404">Category not found.</response>
    [HttpDelete("{categoryId:guid}")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid projectId,
        Guid categoryId,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        var category = await _categoryService.GetByIdAsync(categoryId, ct);
        if (category is null || category.CatProjectId != projectId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["CategoryNotFound"]));

        await _categoryService.SoftDeleteAsync(categoryId, userId, ct);
        return NoContent();
    }
}
