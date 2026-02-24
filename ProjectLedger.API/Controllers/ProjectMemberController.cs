using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Project;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de miembros de un proyecto.
/// 
/// Ruta anidada: /api/projects/{projectId}/members
/// - Viewer+ puede listar miembros.
/// - Owner puede agregar/cambiar rol/remover miembros.
/// - Plan valida CanShareProjects y MaxTeamMembersPerProject.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/members")]
[Authorize]
[Tags("Project Members")]
[Produces("application/json")]
public class ProjectMemberController : ControllerBase
{
    private readonly IProjectMemberService _memberService;
    private readonly IUserService _userService;
    private readonly IProjectAccessService _accessService;

    public ProjectMemberController(
        IProjectMemberService memberService,
        IUserService userService,
        IProjectAccessService accessService)
    {
        _memberService = memberService;
        _userService = userService;
        _accessService = accessService;
    }

    // ── GET /api/projects/{projectId}/members ───────────────

    /// <summary>
    /// Lista todos los miembros del proyecto con su rol.
    /// </summary>
    /// <response code="200">Lista de miembros.</response>
    [HttpGet]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(IEnumerable<ProjectMemberResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers(Guid projectId, CancellationToken ct)
    {
        var members = await _memberService.GetByProjectIdAsync(projectId, ct);
        return Ok(members.ToResponse());
    }

    // ── POST /api/projects/{projectId}/members ──────────────

    /// <summary>
    /// Agrega un miembro al proyecto por email. Solo el owner puede invitar.
    /// Valida Plan:CanShareProjects y MaxTeamMembersPerProject.
    /// </summary>
    /// <response code="201">Miembro agregado.</response>
    /// <response code="400">Usuario ya es miembro.</response>
    /// <response code="403">Sin permisos o límite del plan excedido.</response>
    /// <response code="404">Usuario con ese email no encontrado.</response>
    [HttpPost]
    [Authorize(Policy = "ProjectOwner")]
    [ProducesResponseType(typeof(ProjectMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMember(
        Guid projectId,
        [FromBody] AddProjectMemberRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Buscar usuario por email
        var targetUser = await _userService.GetByEmailAsync(request.Email, ct);
        if (targetUser is null)
            return NotFound(new { message = $"User with email '{request.Email}' not found." });

        // Validar que el rol sea válido (solo editor/viewer, no owner)
        var role = request.Role.ToLowerInvariant();
        if (role is not (ProjectRoles.Editor or ProjectRoles.Viewer))
            return BadRequest(new { message = $"Invalid role '{request.Role}'. Allowed: editor, viewer." });

        var member = new ProjectMember
        {
            PrmId = Guid.NewGuid(),
            PrmProjectId = projectId,
            PrmUserId = targetUser.UsrId,
            PrmRole = role
        };

        await _memberService.AddMemberAsync(member, ct);

        // Reload para obtener nav properties
        var members = await _memberService.GetByProjectIdAsync(projectId, ct);
        var added = members.FirstOrDefault(m => m.PrmUserId == targetUser.UsrId);

        return CreatedAtAction(
            nameof(GetMembers),
            new { projectId },
            added?.ToResponse());
    }

    // ── PUT /api/projects/{projectId}/members/{memberId}/role

    /// <summary>
    /// Cambia el rol de un miembro. Solo el owner puede cambiar roles.
    /// No se puede cambiar el rol del owner.
    /// </summary>
    /// <response code="204">Rol actualizado.</response>
    /// <response code="400">Rol inválido o intento de cambiar al owner.</response>
    /// <response code="404">Miembro no encontrado.</response>
    [HttpPut("{memberId:guid}/role")]
    [Authorize(Policy = "ProjectOwner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRole(
        Guid projectId,
        Guid memberId,
        [FromBody] UpdateMemberRoleRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var role = request.Role.ToLowerInvariant();
        if (role is not (ProjectRoles.Editor or ProjectRoles.Viewer))
            return BadRequest(new { message = $"Invalid role '{request.Role}'. Allowed: editor, viewer." });

        await _memberService.UpdateRoleAsync(memberId, role, ct);
        return NoContent();
    }

    // ── DELETE /api/projects/{projectId}/members/{memberId} ─

    /// <summary>
    /// Remueve un miembro del proyecto. Solo el owner puede remover.
    /// No se puede remover al owner.
    /// </summary>
    /// <response code="204">Miembro removido.</response>
    /// <response code="400">Intento de remover al owner.</response>
    /// <response code="404">Miembro no encontrado.</response>
    [HttpDelete("{memberId:guid}")]
    [Authorize(Policy = "ProjectOwner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(
        Guid projectId,
        Guid memberId,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _memberService.RemoveMemberAsync(memberId, userId, ct);
        return NoContent();
    }
}
