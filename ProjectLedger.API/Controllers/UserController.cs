using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.User;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de perfil de usuario autenticado.
/// 
/// Reglas de seguridad:
/// - Todos los endpoints requieren JWT válido.
/// - UserId se obtiene SIEMPRE del JWT, nunca del body/ruta.
/// - No expone datos sensibles (password hash, tokens, etc.).
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
[Tags("Users")]
[Produces("application/json")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly IEmailService _emailService;

    public UserController(
        IUserService userService,
        IAuthService authService,
        IEmailService emailService)
    {
        _userService = userService;
        _authService = authService;
        _emailService = emailService;
    }

    // ── GET /api/users/profile ──────────────────────────────

    /// <summary>
    /// Obtiene el perfil completo del usuario autenticado (con plan).
    /// </summary>
    /// <response code="200">Perfil del usuario.</response>
    /// <response code="404">Usuario no encontrado.</response>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var user = await _userService.GetByIdAsync(userId, ct);

        if (user is null)
            return NotFound(new { message = "User not found." });

        return Ok(user.ToProfileResponse());
    }

    // ── PUT /api/users/profile ──────────────────────────────

    /// <summary>
    /// Actualiza el perfil del usuario autenticado (nombre, avatar).
    /// UserId se obtiene del JWT — nunca del body.
    /// </summary>
    /// <response code="200">Perfil actualizado.</response>
    /// <response code="404">Usuario no encontrado.</response>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var user = await _userService.GetByIdAsync(userId, ct);

        if (user is null)
            return NotFound(new { message = "User not found." });

        user.ApplyUpdate(request);
        await _userService.UpdateAsync(user, ct);

        return Ok(user.ToProfileResponse());
    }

    // ── PUT /api/users/password ─────────────────────────────

    /// <summary>
    /// Cambia la contraseña del usuario autenticado.
    /// Requiere la contraseña actual para validación.
    /// </summary>
    /// <response code="204">Contraseña cambiada exitosamente.</response>
    /// <response code="400">Contraseña actual incorrecta o request inválido.</response>
    /// <response code="404">Usuario no encontrado.</response>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var user = await _userService.GetByIdAsync(userId, ct);

        if (user is null)
            return NotFound(new { message = "User not found." });

        // Verificar contraseña actual
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.UsrPasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });

        // Hashear y actualizar
        user.UsrPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        await _userService.UpdateAsync(user, ct);

        // Revocar todos los refresh tokens activos (seguridad: invalida todas las sesiones)
        await _authService.RevokeAllTokensAsync(userId, ct);

        // Notificación por correo (fire-and-forget)
        _ = _emailService.SendPasswordChangedEmailAsync(user.UsrEmail, user.UsrFullName, ct);

        return NoContent();
    }

    // ── DELETE /api/users/account ───────────────────────────

    /// <summary>
    /// Soft-delete de la cuenta del usuario autenticado.
    /// Desactiva la cuenta e invalida futuros accesos.
    /// </summary>
    /// <response code="204">Cuenta eliminada.</response>
    [HttpDelete("account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _userService.SoftDeleteAsync(userId, userId, ct);
        return NoContent();
    }
}
