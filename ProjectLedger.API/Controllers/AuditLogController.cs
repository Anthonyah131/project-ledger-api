using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.AuditLog;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de logs de auditoría. Solo lectura.
/// 
/// Solo accesible por el usuario autenticado sobre sus propias acciones,
/// o por admins (por entidad).
/// </summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize]
[Tags("Audit Logs")]
[Produces("application/json")]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    // ── GET /api/audit-logs/me ──────────────────────────────

    /// <summary>
    /// Lista los registros de auditoría de las acciones del usuario autenticado (paginado).
    /// Siempre ordenados por fecha descendente (más recientes primero).
    /// SortBy y SortDirection se ignoran — el orden cronológico es fijo.
    /// </summary>
    /// <response code="200">Lista paginada de registros de auditoría.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(PagedResponse<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAuditLogs(
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var (items, totalCount) = await _auditLogService.GetByUserIdPagedAsync(
            userId, pagination.Skip, pagination.PageSize, ct);

        var response = PagedResponse<AuditLogResponse>.Create(
            items.ToResponse().ToList(), totalCount, pagination);

        return Ok(response);
    }

    // ── GET /api/audit-logs/entity/{entityName}/{entityId} ──

    /// <summary>
    /// Lista los registros de auditoría de una entidad específica (paginado).
    /// Útil para ver el historial de cambios de un project, expense, etc.
    /// Siempre ordenados por fecha descendente. SortBy/SortDirection se ignoran.
    /// </summary>
    /// <param name="entityName">Nombre de la entidad (e.g. Project, Expense, Category).</param>
    /// <param name="entityId">ID de la entidad.</param>
    /// <response code="200">Lista paginada de registros de auditoría.</response>
    [HttpGet("entity/{entityName}/{entityId:guid}")]
    [ProducesResponseType(typeof(PagedResponse<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEntity(
        string entityName,
        Guid entityId,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        var (items, totalCount) = await _auditLogService.GetByEntityPagedAsync(
            entityName, entityId, pagination.Skip, pagination.PageSize, ct);

        var response = PagedResponse<AuditLogResponse>.Create(
            items.ToResponse().ToList(), totalCount, pagination);

        return Ok(response);
    }
}
