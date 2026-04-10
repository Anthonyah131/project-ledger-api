using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.Project;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Project members controller.
/// 
/// Nested route: /api/projects/{projectId}/members
/// - Viewer+ can list members.
/// - Owner can add/change role/remove members.
/// - Plan validates CanShareProjects and MaxTeamMembersPerProject.
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
    private readonly IProjectService _projectService;
    private readonly IEmailService _emailService;
    private readonly IStringLocalizer<Messages> _localizer;

    public ProjectMemberController(
        IProjectMemberService memberService,
        IUserService userService,
        IProjectAccessService accessService,
        IProjectService projectService,
        IEmailService emailService,
        IStringLocalizer<Messages> localizer)
    {
        _memberService = memberService;
        _userService = userService;
        _accessService = accessService;
        _projectService = projectService;
        _emailService = emailService;
        _localizer = localizer;
    }

    // ── GET /api/projects/{projectId}/members ───────────────

    /// <summary>
    /// Lists all project members with their role.
    /// </summary>
    /// <response code="200">List of members.</response>
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
    /// Adds a member to the project by email. Only the owner can invite.
    /// Validates Plan:CanShareProjects and MaxTeamMembersPerProject.
    /// </summary>
    /// <response code="201">Member added.</response>
    /// <response code="400">User is already a member.</response>
    /// <response code="403">No permissions or plan limit exceeded.</response>
    /// <response code="404">User with that email not found.</response>
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

        // Find user by email
        var targetUser = await _userService.GetByEmailAsync(request.Email, ct);
        if (targetUser is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["UserEmailNotFound"]));

        // Validate that the role is valid (only editor/viewer, not owner)
        var role = request.Role.ToLowerInvariant();
        if (role is not (ProjectRoles.Editor or ProjectRoles.Viewer))
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["InvalidRole"]));

        var member = new ProjectMember
        {
            PrmId = Guid.NewGuid(),
            PrmProjectId = projectId,
            PrmUserId = targetUser.UsrId,
            PrmRole = role
        };

        await _memberService.AddMemberAsync(member, ct);

        // Send email notification (fire-and-forget)
        var project = await _projectService.GetByIdAsync(projectId, ct);
        var ownerUser = await _userService.GetByIdAsync(User.GetRequiredUserId(), ct);
        _ = _emailService.SendProjectSharedEmailAsync(
            targetUser.UsrEmail, targetUser.UsrFullName,
            project?.PrjName ?? "Project", role,
            ownerUser?.UsrFullName ?? "Un usuario", ct);

        // Reload to get nav properties
        var members = await _memberService.GetByProjectIdAsync(projectId, ct);
        var added = members.FirstOrDefault(m => m.PrmUserId == targetUser.UsrId);

        return CreatedAtAction(
            nameof(GetMembers),
            new { projectId },
            added?.ToResponse());
    }

    // ── PUT /api/projects/{projectId}/members/{memberId}/role

    /// <summary>
    /// Changes a member's role. Only the owner can change roles.
    /// The owner's role cannot be changed.
    /// </summary>
    /// <response code="204">Role updated.</response>
    /// <response code="400">Invalid role or attempt to change the owner.</response>
    /// <response code="404">Member not found.</response>
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
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["InvalidRole"]));

        await _memberService.UpdateRoleAsync(memberId, role, ct);
        return NoContent();
    }

    // ── DELETE /api/projects/{projectId}/members/{memberId} ─

    /// <summary>
    /// Removes a member from the project. Only the owner can remove.
    /// The owner cannot be removed.
    /// </summary>
    /// <response code="204">Member removed.</response>
    /// <response code="400">Attempt to remove the owner.</response>
    /// <response code="404">Member not found.</response>
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

        // Get member data BEFORE deleting it (for the email)
        var membersBeforeRemoval = await _memberService.GetByProjectIdAsync(projectId, ct);
        var removedMember = membersBeforeRemoval.FirstOrDefault(m => m.PrmId == memberId);

        await _memberService.RemoveMemberAsync(memberId, userId, ct);

        // Send email notification (fire-and-forget)
        if (removedMember?.User is not null)
        {
            var project = await _projectService.GetByIdAsync(projectId, ct);
            var ownerUser = await _userService.GetByIdAsync(userId, ct);
            _ = _emailService.SendProjectAccessRevokedEmailAsync(
                removedMember.User.UsrEmail, removedMember.User.UsrFullName,
                project?.PrjName ?? "Project",
                ownerUser?.UsrFullName ?? "Un usuario", ct);
        }

        return NoContent();
    }
}
