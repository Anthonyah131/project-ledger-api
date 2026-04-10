using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Admin;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// User administration controller.
/// Only accessible by Global Administrators (is_admin = true).
/// 
/// Features:
/// - List all users
/// - View user details
/// - Activate / deactivate user (with email notification)
/// - Edit basic user information
/// - Soft-delete a user
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize]
[Tags("Admin - Users")]
[Produces("application/json")]
public class AdminUserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IStringLocalizer<Messages> _localizer;

    public AdminUserController(IUserService userService,
    IStringLocalizer<Messages> localizer)
    {
        _userService = userService;
        _localizer = localizer;
    }

    // ── Authorization helper ────────────────────────────────

    private bool IsAdmin()
    {
        var claim = User.FindFirst("is_admin")?.Value;
        return claim == "true";
    }

    // ── GET /api/admin/users ────────────────────────────────

    /// <summary>
    /// Lists all system users with pagination (admin only).
    /// </summary>
    /// <param name="pagination">Pagination parameters (page, pageSize, sortBy, sortDirection).</param>
    /// <param name="includeDeleted">If true, includes soft-deleted users.</param>
    /// <response code="200">Paginated list of users.</response>
    /// <response code="403">Not an administrator.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AdminUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedRequest pagination,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        if (!IsAdmin())
            return Forbid();

        var (items, totalCount) = await _userService.GetAllPagedAsync(
            includeDeleted, pagination.Skip, pagination.PageSize,
            pagination.SortBy, pagination.IsDescending, ct);

        var response = PagedResponse<AdminUserResponse>.Create(
            items.Select(u => u.ToAdminResponse()).ToList(), totalCount, pagination);

        return Ok(response);
    }

    // ── GET /api/admin/users/{id} ───────────────────────────

    /// <summary>
    /// Retrieves full details of a user (admin only).
    /// </summary>
    /// <response code="200">User details.</response>
    /// <response code="403">Not an administrator.</response>
    /// <response code="404">User not found.</response>
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["UserNotFound"]));

        return Ok(user.ToAdminResponse());
    }

    // ── PUT /api/admin/users/{id}/activate ──────────────────

    /// <summary>
    /// Activates a user. Sends an email notification to the user.
    /// </summary>
    /// <response code="200">User activated successfully.</response>
    /// <response code="403">Not an administrator.</response>
    /// <response code="404">User not found.</response>
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["UserNotFoundOrDeleted"]));

        return Ok(LocalizedResponse.Create("SUCCESS", _localizer["UserActivatedSuccess"]));
    }

    // ── PUT /api/admin/users/{id}/deactivate ────────────────

    /// <summary>
    /// Deactivates a user. Sends an email notification to the user.
    /// </summary>
    /// <response code="200">User deactivated successfully.</response>
    /// <response code="403">Not an administrator.</response>
    /// <response code="404">User not found.</response>
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["UserNotFoundOrDeleted"]));

        return Ok(LocalizedResponse.Create("SUCCESS", _localizer["UserDeactivatedSuccess"]));
    }

    // ── PUT /api/admin/users/{id} ───────────────────────────

    /// <summary>
    /// Edits basic information of a user (admin only).
    /// </summary>
    /// <response code="200">User updated successfully.</response>
    /// <response code="403">Not an administrator.</response>
    /// <response code="404">User not found.</response>
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["UserNotFound"]));

        user.ApplyAdminUpdate(request);
        await _userService.UpdateAsync(user, ct);

        return Ok(user.ToAdminResponse());
    }

    // ── DELETE /api/admin/users/{id} ────────────────────────

    /// <summary>
    /// Performs a soft-delete on a user. Admin only.
    /// </summary>
    /// <response code="204">User deleted successfully.</response>
    /// <response code="403">Not an administrator.</response>
    /// <response code="404">User not found.</response>
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["UserNotFound"]));
        }
    }
}
