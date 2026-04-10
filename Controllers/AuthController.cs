using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ProjectLedger.API.DTOs.Auth;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using ProjectLedger.API.Services;
using ProjectLedger.API.Common.Settings;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Authentication controller: register, login, refresh, and logout.
/// Rate-limited to prevent brute-force attacks.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("AuthRateLimit")]
[Tags("Auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly GoogleAuthSettings _googleAuthSettings;
    private readonly ILogger<AuthController> _logger;
    private readonly IStringLocalizer<Messages> _localizer;

    public AuthController(
        IAuthService authService,
        IOptions<GoogleAuthSettings> googleAuthSettings,
        ILogger<AuthController> logger,
        IStringLocalizer<Messages> localizer)
    {
        _authService = authService;
        _googleAuthSettings = googleAuthSettings.Value;
        _logger = logger;
        _localizer = localizer;
    }

    // ── POST /api/auth/register ─────────────────────────────

    /// <summary>
    /// Registers a new user. Returns access + refresh tokens.
    /// </summary>
    /// <response code="201">User registered with tokens.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="409">A user with that email already exists.</response>
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
            return Conflict(LocalizedResponse.Create("USER_ALREADY_EXISTS", _localizer["UserAlreadyExists"]));

        return Created(string.Empty, result);
    }

    // ── POST /api/auth/login ────────────────────────────────

    /// <summary>
    /// Authenticates with email/password. Returns access + refresh tokens.
    /// </summary>
    /// <response code="200">Successful login with tokens.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Incorrect email or password.</response>
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
            return Unauthorized(LocalizedResponse.Create("INVALID_CREDENTIALS", _localizer["InvalidCredentials"]));

        return Ok(result);
    }

    // ── GET /api/auth/google/login ─────────────────────────

    /// <summary>
    /// Initiates the OAuth flow with Google.
    /// </summary>
    /// <response code="302">Redirection to Google OAuth.</response>
    /// <response code="400">Google OAuth is not configured.</response>
    [HttpGet("google/login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GoogleLogin()
    {
        if (string.IsNullOrWhiteSpace(_googleAuthSettings.ClientId)
            || string.IsNullOrWhiteSpace(_googleAuthSettings.ClientSecret))
        {
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["GoogleAuthNotConfigured"]));
        }

        var callbackUrl = Url.Action(
            action: nameof(GoogleCallback),
            controller: "Auth",
            values: null,
            protocol: Request.Scheme)
            ?? $"{Request.Scheme}://{Request.Host}/api/auth/google/callback";

        var properties = new AuthenticationProperties
        {
            RedirectUri = callbackUrl
        };

        return Challenge(properties, AuthSchemes.GoogleScheme);
    }

    // ── GET /api/auth/google/callback ──────────────────────

    /// <summary>
    /// Completes the Google OAuth flow, links/creates user and redirects to the frontend with JWT.
    /// </summary>
    /// <response code="302">Redirection to the frontend with the token or error.</response>
    [HttpGet("google/callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> GoogleCallback(CancellationToken ct)
    {
        try
        {
            var authResult = await HttpContext.AuthenticateAsync(AuthSchemes.ExternalCookieScheme);
            if (!authResult.Succeeded || authResult.Principal == null)
            {
                var failureMsg = authResult.Failure?.Message ?? "Unknown auth failure";
                _logger.LogWarning("Google OAuth cookie authentication failed: {Reason}", failureMsg);
                return Redirect(BuildFrontendCallbackUrl(error: "google_auth_failed"));
            }

            var principal = authResult.Principal;

            var googleSub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? principal.FindFirstValue("sub");

            var email = principal.FindFirstValue(ClaimTypes.Email)
                        ?? principal.FindFirstValue("email");

            var fullName = principal.FindFirstValue(ClaimTypes.Name)
                           ?? principal.FindFirstValue("name")
                           ?? email;

            var avatarUrl = principal.FindFirstValue("picture");

            await HttpContext.SignOutAsync(AuthSchemes.ExternalCookieScheme);

            if (string.IsNullOrWhiteSpace(googleSub) || string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("Google OAuth profile incomplete — sub: {Sub}, email: {Email}", googleSub, email);
                return Redirect(BuildFrontendCallbackUrl(error: "google_profile_incomplete"));
            }

            _logger.LogInformation("Google OAuth callback received for email: {Email}", email);

            var accessToken = await _authService.LoginWithGoogleAsync(
                googleSub,
                email,
                fullName ?? email,
                avatarUrl,
                ct);

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("LoginWithGoogleAsync returned null for email: {Email}", email);
                return Redirect(BuildFrontendCallbackUrl(error: "google_login_failed"));
            }

            _logger.LogInformation("Google OAuth login successful for email: {Email}", email);
            return Redirect(BuildFrontendCallbackUrl(token: accessToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GoogleCallback: {Message}", ex.Message);
            return Redirect(BuildFrontendCallbackUrl(error: "google_server_error"));
        }
    }

    // ── POST /api/auth/refresh ──────────────────────────────

    /// <summary>
    /// Renews the access token using a valid refresh token.
    /// The previous refresh token is revoked (rotation).
    /// </summary>
    /// <response code="200">Tokens renewed.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Invalid or expired refresh token.</response>
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
            return Unauthorized(LocalizedResponse.Create("UNAUTHORIZED", _localizer["InvalidRefreshToken"]));

        return Ok(result);
    }

    // ── POST /api/auth/revoke ───────────────────────────────

    /// <summary>
    /// Revokes the active refresh token (logout from the current device).
    /// Requires valid authentication.
    /// </summary>
    /// <response code="200">Token revoked successfully.</response>
    /// <response code="404">Token not found or already revoked.</response>
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["RefreshTokenNotFound"]));

        return Ok(LocalizedResponse.Create("SUCCESS", _localizer["TokenRevokedSuccess"]));
    }

    // ── POST /api/auth/revoke-all ───────────────────────────

    /// <summary>
    /// Revokes ALL user refresh tokens (logout from all devices).
    /// Requires valid authentication. The UserId is obtained from the JWT, NEVER from the body.
    /// </summary>
    /// <response code="200">All tokens revoked.</response>
    [HttpPost("revoke-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeAll(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _authService.RevokeAllTokensAsync(userId, ct);

        return Ok(LocalizedResponse.Create("SUCCESS", _localizer["AllTokensRevokedSuccess"]));
    }

    // ── GET /api/auth/me ────────────────────────────────────

    /// <summary>
    /// Returns the authenticated user information from the JWT.
    /// Does not access the database — only reads claims.
    /// </summary>
    /// <response code="200">Authenticated user information.</response>
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
    /// Verifies if a reset OTP code is valid without consuming it.
    /// Use it to enable the new password step in the frontend only if the OTP is correct.
    /// </summary>
    /// <response code="200">Valid OTP.</response>
    /// <response code="400">Invalid OTP, expired, or email not found.</response>
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
            return BadRequest(LocalizedResponse.Create("INVALID_OTP", _localizer["InvalidOtp"]));

        return Ok(LocalizedResponse.Create("SUCCESS", _localizer["OtpVerifiedSuccess"]));
    }

    // ── POST /api/auth/forgot-password ──────────────────────────────

    /// <summary>
    /// Initiates the password reset flow.
    /// Sends a 6-digit OTP code to the registered email.
    /// Always returns 200 to not reveal if the email exists.
    /// </summary>
    /// <response code="200">Request processed (check email).</response>
    /// <response code="400">Invalid data.</response>
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

        // Generic response: never reveal if the email exists
        return Ok(LocalizedResponse.Create("SUCCESS", _localizer["ForgotPasswordSent"]));
    }

    // ── POST /api/auth/reset-password ───────────────────────────────

    /// <summary>
    /// Resets the password using the OTP code received by email.
    /// </summary>
    /// <response code="200">Password updated successfully.</response>
    /// <response code="400">Invalid data or incorrect/expired OTP code.</response>
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
            return BadRequest(LocalizedResponse.Create("INVALID_OTP", _localizer["InvalidOtp"]));

        return Ok(LocalizedResponse.Create("SUCCESS", _localizer["PasswordUpdatedSuccess"]));
    }

    private string BuildFrontendCallbackUrl(string? token = null, string? error = null)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_googleAuthSettings.FrontendCallbackUrl)
            ? "http://localhost:3000/auth/callback"
            : _googleAuthSettings.FrontendCallbackUrl;

        var callbackUrl = baseUrl;

        if (!string.IsNullOrWhiteSpace(token))
            callbackUrl = QueryHelpers.AddQueryString(callbackUrl, "token", token);

        if (!string.IsNullOrWhiteSpace(error))
            callbackUrl = QueryHelpers.AddQueryString(callbackUrl, "error", error);

        return callbackUrl;
    }
}
