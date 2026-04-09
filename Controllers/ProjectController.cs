using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using ProjectLedger.API.DTOs.Project;
using ProjectLedger.API.DTOs.ProjectPartner;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de proyectos con autorización multi-tenant.
/// 
/// Reglas de seguridad:
/// - GET lista: solo proyectos donde el usuario es owner o miembro
/// - GET/PUT/DELETE por ID: validación de acceso vía IProjectAccessService
/// - POST: el UserId se obtiene SIEMPRE del JWT, nunca del body
/// - ProjectId viene de la ruta, nunca del body
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
    /// Lista todos los proyectos donde el usuario es owner o miembro (paginado).
    /// En la página 1 incluye una sección "pinned" con proyectos fijados (máx. 6).
    /// El total y la paginación normal excluyen los proyectos fijados.
    /// </summary>
    /// <response code="200">Lista paginada de proyectos del usuario con sección de fijados.</response>
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

    // ── PUT /api/projects/{projectId}/pin ───────────────────

    /// <summary>
    /// Fija un proyecto para el usuario autenticado. Máximo 6 proyectos fijados.
    /// </summary>
    /// <response code="200">Proyecto fijado correctamente.</response>
    /// <response code="400">Límite de 6 fijados alcanzado.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
    /// <response code="404">Proyecto no existe o está inactivo.</response>
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
    /// Desancla un proyecto fijado. Operación idempotente.
    /// </summary>
    /// <response code="204">Proyecto desfijado correctamente.</response>
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
    /// Obtiene un proyecto por ID. Valida que el usuario tenga acceso (viewer+).
    /// </summary>
    /// <response code="200">Proyecto encontrado.</response>
    /// <response code="404">Proyecto no encontrado.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
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
    /// Crea un nuevo proyecto. El owner se asigna desde el JWT, NUNCA del body.
    /// Crea automáticamente un ProjectMember con rol "owner".
    /// Valida permisos y límites del plan.
    /// </summary>
    /// <response code="201">Proyecto creado.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="403">Plan no permite crear más proyectos.</response>
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

        // ⚠️ UserId SIEMPRE del JWT — previene escalamiento de privilegios
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
    /// Actualiza un proyecto. Requiere rol editor o superior.
    /// ProjectId viene de la ruta — nunca del body.
    /// </summary>
    /// <response code="200">Proyecto actualizado.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="404">Proyecto no encontrado.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
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

        // Validar plan del owner (y sharing si es miembro compartido)
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
    /// Soft-delete de un proyecto. Solo el owner puede eliminar.
    /// </summary>
    /// <response code="204">Proyecto eliminado.</response>
    /// <response code="403">Solo el owner puede eliminar.</response>
    /// <response code="404">Proyecto no encontrado.</response>
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
    /// Lista los partners asignados al proyecto.
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
    /// Asigna un partner al proyecto. Solo partners del usuario autenticado.
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
    /// Quita un partner del proyecto (soft-delete).
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
    /// Lista los métodos de pago disponibles en el proyecto, agrupados por partner.
    /// Reemplaza GET /projects/:id/payment-methods.
    /// Los métodos se derivan automáticamente de los partners asignados al proyecto.
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
    /// Actualiza configuraciones del proyecto (p.ej. habilitar/deshabilitar partner splits).
    /// Requiere rol owner.
    /// </summary>
    /// <response code="204">Configuración actualizada.</response>
    /// <response code="400">Datos inválidos o condiciones no cumplidas.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
    /// <response code="404">Proyecto no encontrado.</response>
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
    /// Devuelve la distribución equitativa de porcentajes entre los partners del proyecto.
    /// Usar para pre-llenar el formulario de splits al crear/editar un movimiento.
    /// </summary>
    /// <response code="200">Lista de partners con porcentaje por defecto.</response>
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

    /// <summary>Comparer para deduplicar proyectos por PrjId.</summary>
    private class ProjectIdComparer : IEqualityComparer<Project>
    {
        public bool Equals(Project? x, Project? y) => x?.PrjId == y?.PrjId;
        public int GetHashCode(Project obj) => obj.PrjId.GetHashCode();
    }
}