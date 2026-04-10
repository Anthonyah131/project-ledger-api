using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using ProjectLedger.API.DTOs.Project;
using ProjectLedger.API.DTOs.Workspace;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Workspaces controller. A workspace groups projects with a common context.
///
/// Security rules:
/// - Only the owner can modify or delete a workspace.
/// - Users can see their own workspaces or where they are a member.
/// - OwnerUserId is ALWAYS obtained from the JWT.
/// </summary>
[ApiController]
[Route("api/workspaces")]
[Authorize]
[Tags("Workspaces")]
[Produces("application/json")]
public class WorkspaceController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _projectAccessService;
    private readonly IStringLocalizer<Messages> _localizer;

    public WorkspaceController(
        IWorkspaceService workspaceService,
        IProjectService projectService,
        IProjectAccessService projectAccessService,
        IStringLocalizer<Messages> localizer)
    {
        _workspaceService = workspaceService;
        _projectService = projectService;
        _projectAccessService = projectAccessService;
        _localizer = localizer;
    }

    // ── GET /api/workspaces ─────────────────────────────────

    /// <summary>
    /// Lists workspaces where the user is a member or owner.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<WorkspaceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyWorkspaces(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var workspaces = await _workspaceService.GetByMemberUserIdAsync(userId, ct);

        var result = new List<WorkspaceResponse>();
        foreach (var w in workspaces)
        {
            var role = await _workspaceService.GetMemberRoleAsync(w.WksId, userId, ct) ?? "member";
            var count = await _workspaceService.CountProjectsAsync(w.WksId, ct);
            result.Add(w.ToResponse(role, count));
        }

        return Ok(result);
    }

    // ── GET /api/workspaces/{id} ────────────────────────────

    /// <summary>
    /// Gets the detail of a workspace with projects and members.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkspaceDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var workspace = await _workspaceService.GetByIdWithDetailsAsync(id, ct);

        if (workspace is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        var role = await _workspaceService.GetMemberRoleAsync(id, userId, ct);
        if (role is null)
            return Forbid();

        return Ok(workspace.ToDetailResponse(role));
    }

    // ── POST /api/workspaces ────────────────────────────────

    /// <summary>
    /// Creates a new workspace. The creator automatically becomes the 'owner'.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWorkspaceRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var workspace = request.ToEntity(userId);

        await _workspaceService.CreateAsync(workspace, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = workspace.WksId },
            workspace.ToResponse(WorkspaceRoles.Owner, 0));
    }

    // ── PATCH /api/workspaces/{id} ──────────────────────────

    /// <summary>
    /// Updates the workspace. Only the owner can modify it.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(WorkspaceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateWorkspaceRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var workspace = await _workspaceService.GetByIdAsync(id, ct);

        if (workspace is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        if (workspace.WksOwnerUserId != userId)
            return Forbid();

        workspace.ApplyUpdate(request);
        await _workspaceService.UpdateAsync(workspace, ct);

        var count = await _workspaceService.CountProjectsAsync(id, ct);
        return Ok(workspace.ToResponse(WorkspaceRoles.Owner, count));
    }

    // ── DELETE /api/workspaces/{id} ─────────────────────────

    /// <summary>
    /// Soft-delete of the workspace. Only the owner can delete it.
    /// Cannot be deleted if it has active projects.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var workspace = await _workspaceService.GetByIdAsync(id, ct);

        if (workspace is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        if (workspace.WksOwnerUserId != userId)
            return Forbid();

        try
        {
            await _workspaceService.SoftDeleteAsync(id, userId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(LocalizedResponse.Create("CONFLICT", _localizer[ex.Message]));
        }
    }

    // ── GET /api/workspaces/{id}/projects ───────────────────

    /// <summary>
    /// Lists the workspace projects accessible to the authenticated user (paginated).
    /// On page 1 includes a "pinned" section with the pinned projects of this workspace.
    /// The user must be a member of the workspace.
    /// </summary>
    /// <response code="200">Paginated list of workspace projects with a pinned section.</response>
    /// <response code="403">User is not a member of the workspace.</response>
    /// <response code="404">Workspace not found.</response>
    [HttpGet("{id:guid}/projects")]
    [ProducesResponseType(typeof(ProjectsPagedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspaceProjects(
        Guid id,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        var workspace = await _workspaceService.GetByIdAsync(id, ct);
        if (workspace is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        var role = await _workspaceService.GetMemberRoleAsync(id, userId, ct);
        if (role is null)
            return Forbid();

        // Pinned filtered only to the current workspace
        var allPinnedMemberships = await _projectService.GetPinnedMembershipsAsync(userId, ct);
        var workspacePinned = allPinnedMemberships
            .Where(m => m.Project.PrjWorkspaceId == id)
            .ToList();
        var pinnedIds = workspacePinned.Select(m => m.PrmProjectId).ToList();

        var (projects, totalCount) = await _projectService.GetByWorkspaceIdPagedExcludingAsync(
            id, userId, pinnedIds, pagination.Skip, pagination.PageSize, pagination.SortBy, pagination.IsDescending, ct);

        var items = new List<ProjectResponse>();
        foreach (var p in projects)
        {
            var projectRole = await _projectAccessService.GetUserRoleAsync(userId, p.PrjId, ct);
            items.Add(p.ToResponse(projectRole ?? ProjectRoles.Viewer));
        }

        var pinned = new List<PinnedProjectResponse>();
        if (pagination.Page == 1)
        {
            foreach (var m in workspacePinned)
            {
                var projectRole = await _projectAccessService.GetUserRoleAsync(userId, m.PrmProjectId, ct);
                pinned.Add(m.Project.ToPinnedResponse(projectRole ?? ProjectRoles.Viewer, m.PrmPinnedAt!.Value));
            }
        }

        return Ok(new ProjectsPagedResponse
        {
            Pinned = pinned,
            PinnedCount = allPinnedMemberships.Count(),
            Items = items,
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            TotalCount = totalCount
        });
    }

    // ── POST /api/workspaces/{id}/projects ──────────────────

    /// <summary>
    /// Assigns a project to the workspace. The user must be a member of the workspace
    /// and have editor access or higher to the project.
    /// </summary>
    /// <response code="204">Project assigned successfully.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="403">No access to the workspace or the project.</response>
    /// <response code="404">Workspace or project not found.</response>
    [HttpPost("{id:guid}/projects")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignProject(
        Guid id,
        [FromBody] AssignProjectRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();

        // Validate that the user is a member of the workspace
        var workspaceRole = await _workspaceService.GetMemberRoleAsync(id, userId, ct);
        if (workspaceRole is null)
            return Forbid();

        // Validate that the workspace exists
        var workspace = await _workspaceService.GetByIdAsync(id, ct);
        if (workspace is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        // Validate that the user has editor access to the project
        await _projectAccessService.ValidateAccessAsync(userId, request.ProjectId, ProjectRoles.Editor, ct);

        await _projectService.SetWorkspaceAsync(request.ProjectId, id, ct);

        return NoContent();
    }

    // ── DELETE /api/workspaces/{id}/projects/{projectId} ───

    /// <summary>
    /// Unlinks a project from the workspace (sets workspace_id = null).
    /// The user must be a member of the workspace and have editor access to the project.
    /// </summary>
    /// <response code="204">Project unlinked successfully.</response>
    /// <response code="403">No access to the workspace or the project.</response>
    /// <response code="404">Workspace or project not found.</response>
    [HttpDelete("{id:guid}/projects/{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveProject(Guid id, Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        // Validate that the user is a member of the workspace
        var workspaceRole = await _workspaceService.GetMemberRoleAsync(id, userId, ct);
        if (workspaceRole is null)
            return Forbid();

        // Validate that the workspace exists
        var workspace = await _workspaceService.GetByIdAsync(id, ct);
        if (workspace is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        // Validate that the user has editor access to the project
        await _projectAccessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        await _projectService.SetWorkspaceAsync(projectId, null, ct);

        return NoContent();
    }
}
