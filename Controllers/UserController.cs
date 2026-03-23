using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.User;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Resources;
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
    private readonly IStringLocalizer<Messages> _localizer;

    public UserController(
        IUserService userService,
        IAuthService authService,
        IEmailService emailService,
        IStringLocalizer<Messages> localizer)
    {
        _userService = userService;
        _authService = authService;
        _emailService = emailService;
        _localizer = localizer;
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["UserNotFound"]));

        return Ok(user.ToProfileResponse());
    }

    // ── PUT /api/users/profile ──────────────────────────────

    /// <summary>
    /// Actualiza el perfil del usuario autenticado (nombre, avatar).
    /// UserId se obtiene del JWT — nunca del body.
    /// Si avatarUrl no se envía, se conserva el avatar actual.
    /// Si avatarUrl se envía como null, se limpia el avatar.
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["UserNotFound"]));

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
    /// <response code="401">JWT ausente o inválido.</response>
    /// <response code="403">Usuario autenticado sin permiso de escritura (ej: cuenta desactivada).</response>
    /// <response code="404">Usuario no encontrado.</response>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["UserNotFound"]));

        // Verificar contraseña actual
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.UsrPasswordHash))
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["CurrentPasswordIncorrect"]));

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

        // Invalidate refresh tokens to prevent new sessions after account deletion.
        await _authService.RevokeAllTokensAsync(userId, ct);
        await _userService.SoftDeleteAsync(userId, userId, ct);

        return NoContent();
    }
}
