using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Admin;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de administración de usuarios.
/// Solo accesible por Administradores Globales (is_admin = true).
/// 
/// Funcionalidades:
/// - Listar todos los usuarios
/// - Ver detalle de un usuario
/// - Activar / desactivar usuario (con notificación por correo)
/// - Editar información básica de un usuario
/// - Eliminar (soft-delete) un usuario
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize]
[Tags("Admin - Users")]
[Produces("application/json")]
public class AdminUserController : ControllerBase
{
    private readonly IUserService _userService;

    public AdminUserController(IUserService userService)
    {
        _userService = userService;
    }

    // ── Authorization helper ────────────────────────────────

    private bool IsAdmin()
    {
        var claim = User.FindFirst("is_admin")?.Value;
        return claim == "true";
    }

    // ── GET /api/admin/users ────────────────────────────────

    /// <summary>
    /// Lista todos los usuarios del sistema (solo admin).
    /// </summary>
    /// <param name="includeDeleted">Si true, incluye usuarios con soft-delete.</param>
    /// <response code="200">Lista de usuarios.</response>
    /// <response code="403">No es administrador.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        if (!IsAdmin())
            return Forbid();

        var users = await _userService.GetAllAsync(includeDeleted, ct);
        return Ok(users.Select(u => u.ToAdminResponse()));
    }

    // ── GET /api/admin/users/{id} ───────────────────────────

    /// <summary>
    /// Obtiene el detalle completo de un usuario (solo admin).
    /// </summary>
    /// <response code="200">Detalle del usuario.</response>
    /// <response code="403">No es administrador.</response>
    /// <response code="404">Usuario no encontrado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var user = await _userService.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new { message = "User not found." });

        return Ok(user.ToAdminResponse());
    }

    // ── PUT /api/admin/users/{id}/activate ──────────────────

    /// <summary>
    /// Activa un usuario. Envía notificación por correo al usuario.
    /// </summary>
    /// <response code="200">Usuario activado.</response>
    /// <response code="403">No es administrador.</response>
    /// <response code="404">Usuario no encontrado.</response>
    [HttpPut("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var result = await _userService.ActivateAsync(id, ct);
        if (!result)
            return NotFound(new { message = "User not found or deleted." });

        return Ok(new { message = "User activated successfully." });
    }

    // ── PUT /api/admin/users/{id}/deactivate ────────────────

    /// <summary>
    /// Desactiva un usuario. Envía notificación por correo al usuario.
    /// </summary>
    /// <response code="200">Usuario desactivado.</response>
    /// <response code="403">No es administrador.</response>
    /// <response code="404">Usuario no encontrado.</response>
    [HttpPut("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var result = await _userService.DeactivateAsync(id, ct);
        if (!result)
            return NotFound(new { message = "User not found or deleted." });

        return Ok(new { message = "User deactivated successfully." });
    }

    // ── PUT /api/admin/users/{id} ───────────────────────────

    /// <summary>
    /// Edita información básica de un usuario (solo admin).
    /// </summary>
    /// <response code="200">Usuario actualizado.</response>
    /// <response code="403">No es administrador.</response>
    /// <response code="404">Usuario no encontrado.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] AdminUpdateUserRequest request,
        CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userService.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new { message = "User not found." });

        user.ApplyAdminUpdate(request);
        await _userService.UpdateAsync(user, ct);

        return Ok(user.ToAdminResponse());
    }

    // ── DELETE /api/admin/users/{id} ────────────────────────

    /// <summary>
    /// Elimina (soft-delete) un usuario. Solo admin.
    /// </summary>
    /// <response code="204">Usuario eliminado.</response>
    /// <response code="403">No es administrador.</response>
    /// <response code="404">Usuario no encontrado.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!IsAdmin())
            return Forbid();

        var adminId = User.GetRequiredUserId();

        try
        {
            await _userService.SoftDeleteAsync(id, adminId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "User not found." });
        }
    }
}
