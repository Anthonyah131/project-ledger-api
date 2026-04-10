using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Project;
using ProjectLedger.API.Resources;
using ProjectLedger.API.DTOs.ProjectPartner;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Projects controller with multi-tenant authorization.
/// 
/// Security rules:
/// - GET list: only projects where the user is owner or member.
/// - GET/PUT/DELETE by ID: access validation via IProjectAccessService.
/// - POST: the UserId is ALWAYS obtained from the JWT, never from the body.
/// - ProjectId comes from the route, never from the body.
/// </summary>
[ApiController]
[Route("api/projects")]
[Authorize]
[Tags("Projects")]
[Produces("application/json")]
public class ProjectController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _accessService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IProjectPartnerService _projectPartnerService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IStringLocalizer<Messages> _localizer;

    public ProjectController(
        IProjectService projectService,
        IProjectAccessService accessService,
        IPlanAuthorizationService planAuth,
        IProjectPartnerService projectPartnerService,
        IWorkspaceService workspaceService,
        IStringLocalizer<Messages> localizer)
    {
        _projectService = projectService;
        _accessService = accessService;
        _planAuth = planAuth;
        _projectPartnerService = projectPartnerService;
        _workspaceService = workspaceService;
        _localizer = localizer;
    }

    // ── GET /api/projects ───────────────────────────────────

    /// <summary>
    /// Lists all projects where the user is owner or member (paginated).
    /// On page 1, includes a "pinned" section with pinned projects (max 6).
    /// The total and normal pagination exclude pinned projects.
    /// </summary>
    /// <response code="200">Paginated list of user projects with pinned section.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ProjectsPagedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyProjects(
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        // Fetch pinned memberships (always, to know which IDs to exclude)
        var pinnedMemberships = await _projectService.GetPinnedMembershipsAsync(userId, ct);
        var pinnedIds = pinnedMemberships.Select(m => m.PrmProjectId).ToList();

        // Paginated non-pinned projects
        var (projects, totalCount) = await _projectService.GetByUserIdPagedExcludingAsync(
            userId, pinnedIds, pagination.Skip, pagination.PageSize, pagination.SortBy, pagination.IsDescending, ct);

        var items = new List<ProjectResponse>();
        foreach (var p in projects)
        {
            var role = await _accessService.GetUserRoleAsync(userId, p.PrjId, ct);
            items.Add(p.ToResponse(role ?? ProjectRoles.Viewer));
        }

        // Build pinned list only for page 1
        var pinned = new List<PinnedProjectResponse>();
        if (pagination.Page == 1)
        {
            foreach (var m in pinnedMemberships)
            {
                var role = await _accessService.GetUserRoleAsync(userId, m.PrmProjectId, ct);
                pinned.Add(m.Project.ToPinnedResponse(role ?? ProjectRoles.Viewer, m.PrmPinnedAt!.Value));
            }
        }

        return Ok(new ProjectsPagedResponse
        {
            Pinned = pinned,
            PinnedCount = pinnedIds.Count,
            Items = items,
            Page = pagination.Page,
            PageSize = pagination.PageSize,
            TotalCount = totalCount
        });
    }

    // ── GET /api/projects/lookup ────────────────────────────

    /// <summary>
    /// Lightweight, paginated list of the authenticated user's projects.
    /// On page 1, includes pinned projects matching the search.
    /// Designed for command palette, selectors, and pickers.
    /// </summary>
    /// <response code="200">Projects lookup with pinned items on page 1.</response>
    [HttpGet("lookup")]
    [ProducesResponseType(typeof(ProjectsLookupResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookup(
        [FromQuery] LookupRequest request,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        var (pinnedFiltered, pinnedTotalCount, items, totalCount) =
            await _projectService.GetProjectsLookupAsync(
                userId, request.Search, request.Page, request.Skip, request.PageSize, ct);

        var pinned = pinnedFiltered.Select(m => new PinnedProjectLookupItem
        {
            Id = m.Project.PrjId,
            Name = m.Project.PrjName,
            WorkspaceId = m.Project.PrjWorkspaceId,
            WorkspaceName = m.Project.Workspace?.WksName,
            PinnedAt = m.PrmPinnedAt!.Value
        }).ToList();

        var itemList = items.Select(p => new ProjectLookupItem
        {
            Id = p.PrjId,
            Name = p.PrjName,
            WorkspaceId = p.PrjWorkspaceId,
            WorkspaceName = p.Workspace?.WksName
        }).ToList();

        return Ok(new ProjectsLookupResponse
        {
            Pinned = pinned,
            PinnedCount = pinnedTotalCount,
            Items = itemList,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        });
    }

    // ── PUT /api/projects/{projectId}/pin ───────────────────

    /// <summary>
    /// Pins a project for the authenticated user. Maximum of 6 pinned projects.
    /// </summary>
    /// <response code="200">Project successfully pinned.</response>
    /// <response code="400">Limit of 6 pinned projects reached.</response>
    /// <response code="403">No access to the project.</response>
    /// <response code="404">Project does not exist or is inactive.</response>
    [HttpPut("{projectId:guid}/pin")]
    [ProducesResponseType(typeof(PinProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PinProject(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Viewer, ct);

        try
        {
            var pinnedAt = await _projectService.PinProjectAsync(userId, projectId, ct);
            return Ok(new PinProjectResponse { ProjectId = projectId, PinnedAt = pinnedAt });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ProjectNotFound"]));
        }
        catch (InvalidOperationException ex) when (ex.Message == "PINNED_LIMIT_EXCEEDED")
        {
            return BadRequest(new { type = "BusinessError", code = "PINNED_LIMIT_EXCEEDED", message = "Maximum of 6 pinned projects reached.", limit = 6 });
        }
    }

    // ── DELETE /api/projects/{projectId}/pin ────────────────

    /// <summary>
    /// Unpins a pinned project. Idempotent operation.
    /// </summary>
    /// <response code="204">Project successfully unpinned.</response>
    [HttpDelete("{projectId:guid}/pin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnpinProject(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _projectService.UnpinProjectAsync(userId, projectId, ct);
        return NoContent();
    }

    // ── GET /api/projects/{projectId} ───────────────────────

    /// <summary>
    /// Gets a project by ID. Validates user access (viewer+).
    /// </summary>
    /// <response code="200">Project found.</response>
    /// <response code="404">Project not found.</response>
    /// <response code="403">No access to the project.</response>
    [HttpGet("{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Viewer, ct);

        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project == null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ProjectNotFound"]));

        var role = await _accessService.GetUserRoleAsync(userId, projectId, ct);
        return Ok(project.ToResponse(role ?? ProjectRoles.Viewer));
    }

    // ── POST /api/projects ──────────────────────────────────

    /// <summary>
    /// Creates a new project. The owner is assigned from the JWT, NEVER from the body.
    /// Automatically creates a ProjectMember with the "owner" role.
    /// Validates permissions and plan limits.
    /// </summary>
    /// <response code="201">Project created.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="403">Plan does not allow creating more projects.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProjectRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // ⚠️ UserId ALWAYS from the JWT — prevents privilege escalation
        var userId = User.GetRequiredUserId();

        // Resolve workspace_id: use provided one (validating membership) or fall back to "General"
        Guid? resolvedWorkspaceId = null;

        if (request.WorkspaceId.HasValue)
        {
            var role = await _workspaceService.GetMemberRoleAsync(request.WorkspaceId.Value, userId, ct);
            if (role is null)
                return Forbid();
            resolvedWorkspaceId = request.WorkspaceId.Value;
        }
        else
        {
            var generalWorkspace = await _workspaceService.GetGeneralWorkspaceForUserAsync(userId, ct);
            resolvedWorkspaceId = generalWorkspace?.WksId;
        }

        var project = request.ToEntity(userId);
        project.PrjWorkspaceId = resolvedWorkspaceId;

        await _projectService.CreateAsync(project, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { projectId = project.PrjId },
            project.ToResponse(ProjectRoles.Owner));
    }

    // ── PUT /api/projects/{projectId} ───────────────────────

    /// <summary>
    /// Updates a project. Requires editor role or higher.
    /// ProjectId comes from the route — never from the body.
    /// </summary>
    /// <response code="200">Project updated.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="404">Project not found.</response>
    /// <response code="403">No access to the project.</response>
    [HttpPut("{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        Guid projectId,
        [FromBody] UpdateProjectRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();

        // Validate owner's plan (and sharing if shared member)
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project == null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ProjectNotFound"]));

        project.ApplyUpdate(request);
        await _projectService.UpdateAsync(project, ct);

        var role = await _accessService.GetUserRoleAsync(userId, projectId, ct);
        return Ok(project.ToResponse(role ?? ProjectRoles.Editor));
    }

    // ── DELETE /api/projects/{projectId} ────────────────────

    /// <summary>
    /// Soft-deletes a project. Only the owner can delete.
    /// </summary>
    /// <response code="204">Project deleted.</response>
    /// <response code="403">Only the owner can delete.</response>
    /// <response code="404">Project not found.</response>
    [HttpDelete("{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Owner, ct);

        await _projectService.SoftDeleteAsync(projectId, userId, ct);

        return NoContent();
    }

    // ── GET /api/projects/{projectId}/partners ───────────────

    /// <summary>
    /// Lists the partners assigned to the project.
    /// </summary>
    [HttpGet("{projectId:guid}/partners")]
    [ProducesResponseType(typeof(IEnumerable<ProjectPartnerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectPartners(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Viewer, ct);

        var partners = await _projectPartnerService.GetByProjectIdAsync(projectId, ct);

        var result = partners.Select(p => new ProjectPartnerResponse
        {
            Id = p.PtpId,
            PartnerId = p.PtpPartnerId,
            PartnerName = p.Partner.PtrName,
            PartnerEmail = p.Partner.PtrEmail,
            AddedAt = p.PtpCreatedAt
        });

        return Ok(result);
    }

    // ── POST /api/projects/{projectId}/partners ──────────────

    /// <summary>
    /// Assigns a partner to the project. Only authenticated user's partners are allowed.
    /// </summary>
    [HttpPost("{projectId:guid}/partners")]
    [ProducesResponseType(typeof(ProjectPartnerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddProjectPartner(
        Guid projectId,
        [FromBody] AddProjectPartnerRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        try
        {
            var assignment = await _projectPartnerService.AddAsync(projectId, request.PartnerId, userId, ct);

            var response = new ProjectPartnerResponse
            {
                Id = assignment.PtpId,
                PartnerId = assignment.PtpPartnerId,
                PartnerName = assignment.Partner?.PtrName ?? string.Empty,
                PartnerEmail = assignment.Partner?.PtrEmail,
                AddedAt = assignment.PtpCreatedAt
            };

            return CreatedAtAction(
                nameof(GetProjectPartners),
                new { projectId },
                response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer[ex.Message]));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(LocalizedResponse.Create("CONFLICT", _localizer[ex.Message]));
        }
    }

    // ── DELETE /api/projects/{projectId}/partners/{partnerId} ─

    /// <summary>
    /// Removes a partner from the project (soft-delete).
    /// </summary>
    [HttpDelete("{projectId:guid}/partners/{partnerId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveProjectPartner(
        Guid projectId, Guid partnerId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        try
        {
            await _projectPartnerService.RemoveAsync(projectId, partnerId, userId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer[ex.Message]));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(LocalizedResponse.Create("CONFLICT", _localizer[ex.Message]));
        }
    }

    // ── GET /api/projects/{projectId}/available-payment-methods

    /// <summary>
    /// Lists the available payment methods in the project, grouped by partner.
    /// Replaces GET /projects/:id/payment-methods.
    /// Methods are automatically derived from the partners assigned to the project.
    /// </summary>
    [HttpGet("{projectId:guid}/available-payment-methods")]
    [ProducesResponseType(typeof(AvailablePaymentMethodsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailablePaymentMethods(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Viewer, ct);

        var paymentMethods = await _projectPartnerService.GetAvailablePaymentMethodsAsync(projectId, userId, ct);

        var unpartnered = paymentMethods
            .Where(pm => pm.OwnerPartner is null)
            .Select(pm => new ProjectPaymentMethodItem
            {
                Id = pm.PmtId,
                Name = pm.PmtName,
                Type = pm.PmtType,
                Currency = pm.PmtCurrency,
                BankName = pm.PmtBankName
            })
            .ToList();

        var grouped = paymentMethods
            .Where(pm => pm.OwnerPartner is not null)
            .GroupBy(pm => pm.OwnerPartner!)
            .Select(g => new PartnerWithPaymentMethods
            {
                PartnerId = g.Key.PtrId,
                PartnerName = g.Key.PtrName,
                PaymentMethods = g.Select(pm => new ProjectPaymentMethodItem
                {
                    Id = pm.PmtId,
                    Name = pm.PmtName,
                    Type = pm.PmtType,
                    Currency = pm.PmtCurrency,
                    BankName = pm.PmtBankName
                }).ToList()
            })
            .ToList();

        return Ok(new AvailablePaymentMethodsResponse
        {
            ProjectId = projectId,
            UnpartneredPaymentMethods = unpartnered,
            Partners = grouped
        });
    }

    // ── PATCH /api/projects/{projectId}/settings ─────────────

    /// <summary>
    /// Updates project settings (e.g. enable/disable partner splits).
    /// Requires owner role.
    /// </summary>
    /// <response code="204">Settings updated.</response>
    /// <response code="400">Invalid data or conditions not met.</response>
    /// <response code="403">No access to the project.</response>
    /// <response code="404">Project not found.</response>
    [HttpPatch("{projectId:guid}/settings")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSettings(
        Guid projectId,
        [FromBody] UpdateProjectSettingsRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Owner, ct);

        try
        {
            await _projectService.UpdateSettingsAsync(projectId, request.PartnersEnabled, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer[ex.Message]));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer[ex.Message]));
        }
    }

    // ── GET /api/projects/{projectId}/partners/split-defaults ─

    /// <summary>
    /// Returns the equal distribution of percentages among the project's partners.
    /// Use to pre-fill the splits form when creating/editing a movement.
    /// </summary>
    /// <response code="200">List of partners with their default percentage.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
    [HttpGet("{projectId:guid}/partners/split-defaults")]
    [ProducesResponseType(typeof(SplitDefaultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSplitDefaults(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Viewer, ct);

        var defaults = await _projectPartnerService.GetSplitDefaultsAsync(projectId, ct);

        return Ok(new SplitDefaultsResponse
        {
            Partners = defaults.Select(d => new PartnerSplitDefault
            {
                PartnerId = d.PartnerId,
                Name = d.Name,
                DefaultPercentage = d.DefaultPercentage
            }).ToList()
        });
    }

    // ── Private Helpers ─────────────────────────────────────

    /// <summary>Comparer to deduplicate projects by PrjId.</summary>
    private class ProjectIdComparer : IEqualityComparer<Project>
    {
        public bool Equals(Project? x, Project? y) => x?.PrjId == y?.PrjId;
        public int GetHashCode(Project obj) => obj.PrjId.GetHashCode();
    }
}