using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using ProjectLedger.API.DTOs.Project;
using ProjectLedger.API.DTOs.Workspace;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de workspaces. Un workspace agrupa proyectos con contexto común.
///
/// Reglas de seguridad:
/// - Solo el owner puede modificar o eliminar un workspace.
/// - Solo el owner puede ver workspaces propios o donde es miembro.
/// - OwnerUserId se obtiene SIEMPRE del JWT.
/// </summary>
[ApiController]
[Route("api/workspaces")]
[Authorize]
[Tags("Workspaces")]
[Produces("application/json")]
public class WorkspaceController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _projectAccessService;
    private readonly IStringLocalizer<Messages> _localizer;

    public WorkspaceController(
        IWorkspaceService workspaceService,
        IProjectService projectService,
        IProjectAccessService projectAccessService,
        IStringLocalizer<Messages> localizer)
    {
        _workspaceService = workspaceService;
        _projectService = projectService;
        _projectAccessService = projectAccessService;
        _localizer = localizer;
    }

    // ── GET /api/workspaces ─────────────────────────────────

    /// <summary>
    /// Lista los workspaces donde el usuario es miembro o owner.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<WorkspaceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyWorkspaces(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var workspaces = await _workspaceService.GetByMemberUserIdAsync(userId, ct);

        var result = new List<WorkspaceResponse>();
        foreach (var w in workspaces)
        {
            var role = await _workspaceService.GetMemberRoleAsync(w.WksId, userId, ct) ?? "member";
            var count = await _workspaceService.CountProjectsAsync(w.WksId, ct);
            result.Add(w.ToResponse(role, count));
        }

        return Ok(result);
    }

    // ── GET /api/workspaces/{id} ────────────────────────────

    /// <summary>
    /// Obtiene el detalle de un workspace con proyectos y miembros.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkspaceDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var workspace = await _workspaceService.GetByIdWithDetailsAsync(id, ct);

        if (workspace is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        var role = await _workspaceService.GetMemberRoleAsync(id, userId, ct);
        if (role is null)
            return Forbid();

        return Ok(workspace.ToDetailResponse(role));
    }

    // ── POST /api/workspaces ────────────────────────────────

    /// <summary>
    /// Crea un nuevo workspace. El creador queda como 'owner' automáticamente.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWorkspaceRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var workspace = request.ToEntity(userId);

        await _workspaceService.CreateAsync(workspace, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = workspace.WksId },
            workspace.ToResponse(WorkspaceRoles.Owner, 0));
    }

    // ── PATCH /api/workspaces/{id} ──────────────────────────

    /// <summary>
    /// Actualiza el workspace. Solo el owner puede modificarlo.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(WorkspaceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateWorkspaceRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var workspace = await _workspaceService.GetByIdAsync(id, ct);

        if (workspace is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        if (workspace.WksOwnerUserId != userId)
            return Forbid();

        workspace.ApplyUpdate(request);
        await _workspaceService.UpdateAsync(workspace, ct);

        var count = await _workspaceService.CountProjectsAsync(id, ct);
        return Ok(workspace.ToResponse(WorkspaceRoles.Owner, count));
    }

    // ── DELETE /api/workspaces/{id} ─────────────────────────

    /// <summary>
    /// Soft-delete del workspace. Solo el owner puede eliminarlo.
    /// No se puede eliminar si tiene proyectos activos.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var workspace = await _workspaceService.GetByIdAsync(id, ct);

        if (workspace is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        if (workspace.WksOwnerUserId != userId)
            return Forbid();

        try
        {
            await _workspaceService.SoftDeleteAsync(id, userId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(LocalizedResponse.Create("CONFLICT", _localizer[ex.Message]));
        }
    }

    // ── GET /api/workspaces/{id}/projects ───────────────────

    /// <summary>
    /// Lista los proyectos del workspace accesibles para el usuario autenticado (paginado).
    /// El usuario debe ser miembro del workspace.
    /// </summary>
    /// <response code="200">Lista paginada de proyectos del workspace.</response>
    /// <response code="403">El usuario no es miembro del workspace.</response>
    /// <response code="404">Workspace no encontrado.</response>
    [HttpGet("{id:guid}/projects")]
    [ProducesResponseType(typeof(PagedResponse<ProjectResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspaceProjects(
        Guid id,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        var workspace = await _workspaceService.GetByIdAsync(id, ct);
        if (workspace is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        var role = await _workspaceService.GetMemberRoleAsync(id, userId, ct);
        if (role is null)
            return Forbid();

        var (projects, totalCount) = await _projectService.GetByWorkspaceIdPagedAsync(
            id, userId, pagination.Skip, pagination.PageSize, pagination.SortBy, pagination.IsDescending, ct);

        var result = new List<ProjectResponse>();
        foreach (var p in projects)
        {
            var projectRole = await _projectAccessService.GetUserRoleAsync(userId, p.PrjId, ct);
            result.Add(p.ToResponse(projectRole ?? ProjectRoles.Viewer));
        }

        return Ok(PagedResponse<ProjectResponse>.Create(result, totalCount, pagination));
    }

    // ── POST /api/workspaces/{id}/projects ──────────────────

    /// <summary>
    /// Asigna un proyecto al workspace. El usuario debe ser miembro del workspace
    /// y tener acceso de editor o superior al proyecto.
    /// </summary>
    /// <response code="204">Proyecto asignado correctamente.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="403">Sin acceso al workspace o al proyecto.</response>
    /// <response code="404">Workspace o proyecto no encontrado.</response>
    [HttpPost("{id:guid}/projects")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignProject(
        Guid id,
        [FromBody] AssignProjectRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();

        // Validar que el usuario es miembro del workspace
        var workspaceRole = await _workspaceService.GetMemberRoleAsync(id, userId, ct);
        if (workspaceRole is null)
            return Forbid();

        // Validar que el workspace existe
        var workspace = await _workspaceService.GetByIdAsync(id, ct);
        if (workspace is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        // Validar que el usuario tiene acceso de editor al proyecto
        await _projectAccessService.ValidateAccessAsync(userId, request.ProjectId, ProjectRoles.Editor, ct);

        await _projectService.SetWorkspaceAsync(request.ProjectId, id, ct);

        return NoContent();
    }

    // ── DELETE /api/workspaces/{id}/projects/{projectId} ───

    /// <summary>
    /// Desvincula un proyecto del workspace (establece workspace_id = null).
    /// El usuario debe ser miembro del workspace y tener acceso de editor al proyecto.
    /// </summary>
    /// <response code="204">Proyecto desvinculado correctamente.</response>
    /// <response code="403">Sin acceso al workspace o al proyecto.</response>
    /// <response code="404">Workspace o proyecto no encontrado.</response>
    [HttpDelete("{id:guid}/projects/{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveProject(Guid id, Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        // Validar que el usuario es miembro del workspace
        var workspaceRole = await _workspaceService.GetMemberRoleAsync(id, userId, ct);
        if (workspaceRole is null)
            return Forbid();

        // Validar que el workspace existe
        var workspace = await _workspaceService.GetByIdAsync(id, ct);
        if (workspace is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        // Validar que el usuario tiene acceso de editor al proyecto
        await _projectAccessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        await _projectService.SetWorkspaceAsync(projectId, null, ct);

        return NoContent();
    }
}
