using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Project;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de proyectos con autorización multi-tenant.
/// 
/// Reglas de seguridad:
/// - GET lista: solo proyectos donde el usuario es owner o miembro
/// - GET/PUT/DELETE por ID: validación de acceso vía IProjectAccessService
/// - POST: el UserId se obtiene SIEMPRE del JWT, nunca del body
/// - ProjectId viene de la ruta, nunca del body
/// </summary>
[ApiController]
[Route("api/projects")]
[Authorize]
[Tags("Projects")]
[Produces("application/json")]
public class ProjectController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _accessService;
    private readonly IPlanAuthorizationService _planAuth;

    public ProjectController(
        IProjectService projectService,
        IProjectAccessService accessService,
        IPlanAuthorizationService planAuth)
    {
        _projectService = projectService;
        _accessService = accessService;
        _planAuth = planAuth;
    }

    // ── GET /api/projects ───────────────────────────────────

    /// <summary>
    /// Lista todos los proyectos donde el usuario es owner o miembro.
    /// </summary>
    /// <response code="200">Lista de proyectos del usuario.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProjectResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyProjects(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        var memberProjects = await _projectService.GetByMemberUserIdAsync(userId, ct);
        var ownedProjects = await _projectService.GetByOwnerUserIdAsync(userId, ct);

        // Unir sin duplicados
        var allProjects = ownedProjects
            .Union(memberProjects, new ProjectIdComparer())
            .ToList();

        var result = new List<ProjectResponse>();
        foreach (var p in allProjects)
        {
            var role = await _accessService.GetUserRoleAsync(userId, p.PrjId, ct);
            result.Add(p.ToResponse(role ?? ProjectRoles.Viewer));
        }

        return Ok(result);
    }

    // ── GET /api/projects/{projectId} ───────────────────────

    /// <summary>
    /// Obtiene un proyecto por ID. Valida que el usuario tenga acceso (viewer+).
    /// </summary>
    /// <response code="200">Proyecto encontrado.</response>
    /// <response code="404">Proyecto no encontrado.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
    [HttpGet("{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Viewer, ct);

        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project == null)
            return NotFound(new { message = "Project not found." });

        var role = await _accessService.GetUserRoleAsync(userId, projectId, ct);
        return Ok(project.ToResponse(role ?? ProjectRoles.Viewer));
    }

    // ── POST /api/projects ──────────────────────────────────

    /// <summary>
    /// Crea un nuevo proyecto. El owner se asigna desde el JWT, NUNCA del body.
    /// Crea automáticamente un ProjectMember con rol "owner".
    /// Valida permisos y límites del plan.
    /// </summary>
    /// <response code="201">Proyecto creado.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="403">Plan no permite crear más proyectos.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProjectRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // ⚠️ UserId SIEMPRE del JWT — previene escalamiento de privilegios
        var userId = User.GetRequiredUserId();

        var project = request.ToEntity(userId);
        await _projectService.CreateAsync(project, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { projectId = project.PrjId },
            project.ToResponse(ProjectRoles.Owner));
    }

    // ── PUT /api/projects/{projectId} ───────────────────────

    /// <summary>
    /// Actualiza un proyecto. Requiere rol editor o superior.
    /// ProjectId viene de la ruta — nunca del body.
    /// </summary>
    /// <response code="200">Proyecto actualizado.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="404">Proyecto no encontrado.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
    [HttpPut("{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        Guid projectId,
        [FromBody] UpdateProjectRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();

        // Validar plan del owner (y sharing si es miembro compartido)
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project == null)
            return NotFound(new { message = "Project not found." });

        project.ApplyUpdate(request);
        await _projectService.UpdateAsync(project, ct);

        var role = await _accessService.GetUserRoleAsync(userId, projectId, ct);
        return Ok(project.ToResponse(role ?? ProjectRoles.Editor));
    }

    // ── DELETE /api/projects/{projectId} ────────────────────

    /// <summary>
    /// Soft-delete de un proyecto. Solo el owner puede eliminar.
    /// </summary>
    /// <response code="204">Proyecto eliminado.</response>
    /// <response code="403">Solo el owner puede eliminar.</response>
    /// <response code="404">Proyecto no encontrado.</response>
    [HttpDelete("{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Owner, ct);

        await _projectService.SoftDeleteAsync(projectId, userId, ct);

        return NoContent();
    }

    // ── Private Helpers ─────────────────────────────────────

    /// <summary>Comparer para deduplicar proyectos por PrjId.</summary>
    private class ProjectIdComparer : IEqualityComparer<Project>
    {
        public bool Equals(Project? x, Project? y) => x?.PrjId == y?.PrjId;
        public int GetHashCode(Project obj) => obj.PrjId.GetHashCode();
    }
}
