using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectLedger.API.DTOs.Auth;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de autenticación: registro, login, refresh y logout.
/// Rate-limited para prevenir ataques de fuerza bruta.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("AuthRateLimit")]
[Tags("Auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // ── POST /api/auth/register ─────────────────────────────

    /// <summary>
    /// Registra un nuevo usuario. Retorna access + refresh tokens.
    /// </summary>
    /// <response code="201">Usuario registrado con tokens.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="409">Ya existe un usuario con ese email.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.RegisterAsync(request, ct);
        if (result == null)
            return Conflict(new { message = "A user with this email already exists." });

        return Created(string.Empty, result);
    }

    // ── POST /api/auth/login ────────────────────────────────

    /// <summary>
    /// Autentica con email/password. Retorna access + refresh tokens.
    /// </summary>
    /// <response code="200">Login exitoso con tokens.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="401">Email o contraseña incorrectos.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.LoginAsync(request, ct);
        if (result == null)
            return Unauthorized(new { message = "Invalid email or password." });

        return Ok(result);
    }

    // ── POST /api/auth/refresh ──────────────────────────────

    /// <summary>
    /// Renueva el access token usando un refresh token válido.
    /// El refresh token anterior queda revocado (rotación).
    /// </summary>
    /// <response code="200">Tokens renovados.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="401">Refresh token inválido o expirado.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.RefreshTokenAsync(request.RefreshToken, ct);
        if (result == null)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        return Ok(result);
    }

    // ── POST /api/auth/revoke ───────────────────────────────

    /// <summary>
    /// Revoca el refresh token activo (logout del dispositivo actual).
    /// Requiere autenticación válida.
    /// </summary>
    /// <response code="200">Token revocado exitosamente.</response>
    /// <response code="404">Token no encontrado o ya revocado.</response>
    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        [FromBody] RevokeTokenRequest request,
        CancellationToken ct)
    {
        var success = await _authService.RevokeTokenAsync(request.RefreshToken, ct);
        if (!success)
            return NotFound(new { message = "Refresh token not found or already revoked." });

        return Ok(new { message = "Token revoked successfully." });
    }

    // ── POST /api/auth/revoke-all ───────────────────────────

    /// <summary>
    /// Revoca TODOS los refresh tokens del usuario (logout de todos los dispositivos).
    /// Requiere autenticación válida. El UserId se obtiene del JWT, NUNCA del body.
    /// </summary>
    /// <response code="200">Todos los tokens revocados.</response>
    [HttpPost("revoke-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeAll(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _authService.RevokeAllTokensAsync(userId, ct);

        return Ok(new { message = "All tokens revoked successfully." });
    }

    // ── GET /api/auth/me ────────────────────────────────────

    /// <summary>
    /// Retorna la información del usuario autenticado desde el JWT.
    /// No accede a la base de datos — solo lee claims.
    /// </summary>
    /// <response code="200">Información del usuario autenticado.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId = User.GetRequiredUserId(),
            email  = User.GetEmail()
        });
    }
    // ── POST /api/auth/verify-otp ────────────────────────────────────

    /// <summary>
    /// Verifica si un código OTP de restablecimiento es válido sin consumirlo.
    /// Úsalo para habilitar el paso de nueva contraseña en el frontend solo si el OTP es correcto.
    /// </summary>
    /// <response code="200">OTP válido.</response>
    /// <response code="400">OTP inválido, expirado o email no encontrado.</response>
    [HttpPost("verify-otp")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var valid = await _authService.VerifyOtpAsync(request.Email, request.OtpCode, ct);
        if (!valid)
            return BadRequest(new { message = "Invalid, expired, or already used OTP code." });

        return Ok(new { message = "OTP verified successfully." });
    }

    // ── POST /api/auth/forgot-password ──────────────────────────────

    /// <summary>
    /// Inicia el flujo de restablecimiento de contraseña.
    /// Envía un código OTP de 6 dígitos al correo registrado.
    /// Siempre retorna 200 para no revelar si el email existe.
    /// </summary>
    /// <response code="200">Solicitud procesada (ver correo).</response>
    /// <response code="400">Datos inválidos.</response>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _authService.ForgotPasswordAsync(request.Email, ct);

        // Respuesta genérica: nunca revelar si el email existe
        return Ok(new { message = "If that email is registered, you will receive a reset code shortly." });
    }

    // ── POST /api/auth/reset-password ───────────────────────────────

    /// <summary>
    /// Restablece la contraseña usando el código OTP recibido por correo.
    /// </summary>
    /// <response code="200">Contraseña actualizada correctamente.</response>
    /// <response code="400">Datos inválidos o código OTP incorrecto/expirado.</response>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var success = await _authService.ResetPasswordAsync(request, ct);
        if (!success)
            return BadRequest(new { message = "Invalid, expired, or already used OTP code." });

        return Ok(new { message = "Password updated successfully." });
    }}
