using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Category;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de categorías de un proyecto.
/// 
/// Ruta anidada: /api/projects/{projectId}/categories
/// - ProjectId viene SIEMPRE de la ruta, nunca del body.
/// - Viewer+ puede listar/ver. Editor+ puede crear/editar/eliminar.
/// - Plan valida MaxCategoriesPerProject al crear.
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

    public CategoryController(
        ICategoryService categoryService,
        IProjectAccessService accessService,
        IPlanAuthorizationService planAuth)
    {
        _categoryService = categoryService;
        _accessService = accessService;
        _planAuth = planAuth;
    }

    // ── GET /api/projects/{projectId}/categories ────────────

    /// <summary>
    /// Lista todas las categorías del proyecto.
    /// </summary>
    /// <response code="200">Lista de categorías.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
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
    /// Obtiene una categoría por ID.
    /// </summary>
    /// <response code="200">Categoría encontrada.</response>
    /// <response code="404">Categoría no encontrada o no pertenece al proyecto.</response>
    [HttpGet("{categoryId:guid}")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid projectId, Guid categoryId, CancellationToken ct)
    {
        var category = await _categoryService.GetByIdAsync(categoryId, ct);
        if (category is null || category.CatProjectId != projectId)
            return NotFound(new { message = "Category not found in this project." });

        return Ok(category.ToResponse());
    }

    // ── POST /api/projects/{projectId}/categories ───────────

    /// <summary>
    /// Crea una categoría en el proyecto. Requiere editor+.
    /// Valida límite MaxCategoriesPerProject del plan del owner.
    /// </summary>
    /// <response code="201">Categoría creada.</response>
    /// <response code="403">Sin permisos o límite del plan excedido.</response>
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
    /// Actualiza una categoría. Requiere editor+.
    /// </summary>
    /// <response code="200">Categoría actualizada.</response>
    /// <response code="404">Categoría no encontrada.</response>
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
            return NotFound(new { message = "Category not found in this project." });

        category.ApplyUpdate(request);
        await _categoryService.UpdateAsync(category, ct);

        return Ok(category.ToResponse());
    }

    // ── DELETE /api/projects/{projectId}/categories/{categoryId}

    /// <summary>
    /// Soft-delete de una categoría. Requiere editor+.
    /// </summary>
    /// <response code="204">Categoría eliminada.</response>
    /// <response code="404">Categoría no encontrada.</response>
    [HttpDelete("{categoryId:guid}")]
    [Authorize(Policy = "ProjectEditor")]
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
            return NotFound(new { message = "Category not found in this project." });

        await _categoryService.SoftDeleteAsync(categoryId, userId, ct);
        return NoContent();
    }
}
