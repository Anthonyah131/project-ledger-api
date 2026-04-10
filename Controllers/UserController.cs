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
/// Authenticated user profile controller.
/// 
/// Security rules:
/// - All endpoints require a valid JWT.
/// - UserId is ALWAYS obtained from the JWT, never from the body/route.
/// - Does not expose sensitive data (password hash, tokens, etc.).
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
    /// Gets the complete profile of the authenticated user (with plan).
    /// </summary>
    /// <response code="200">User profile.</response>
    /// <response code="404">User not found.</response>
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
    /// Updates the authenticated user's profile (name, avatar).
    /// UserId is obtained from the JWT — never from the body.
    /// If avatarUrl is not sent, the current avatar is kept.
    /// If avatarUrl is sent as null, the avatar is cleared.
    /// </summary>
    /// <response code="200">Updated profile.</response>
    /// <response code="404">User not found.</response>
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
    /// Changes the password of the authenticated user.
    /// Requires the current password for validation.
    /// </summary>
    /// <response code="204">Password changed successfully.</response>
    /// <response code="400">Current password incorrect or invalid request.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Authenticated user without write permission (e.g. deactivated account).</response>
    /// <response code="404">User not found.</response>
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

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.UsrPasswordHash))
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["CurrentPasswordIncorrect"]));

        // Hash and update
        user.UsrPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        await _userService.UpdateAsync(user, ct);

        // Revoke all active refresh tokens (security: invalidates all sessions)
        await _authService.RevokeAllTokensAsync(userId, ct);

        // Email notification (fire-and-forget)
        _ = _emailService.SendPasswordChangedEmailAsync(user.UsrEmail, user.UsrFullName, ct);

        return NoContent();
    }

    // ── DELETE /api/users/account ───────────────────────────

    /// <summary>
    /// Soft-delete of the authenticated user's account.
    /// Deactivates the account and invalidates future accesses.
    /// </summary>
    /// <response code="204">Account deleted.</response>
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
