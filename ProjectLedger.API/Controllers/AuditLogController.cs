using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.AuditLog;
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
    /// Lista los registros de auditoría de las acciones del usuario autenticado.
    /// </summary>
    /// <response code="200">Lista de registros de auditoría.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(IEnumerable<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAuditLogs(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var logs = await _auditLogService.GetByUserIdAsync(userId, ct);
        return Ok(logs.ToResponse());
    }

    // ── GET /api/audit-logs/entity/{entityName}/{entityId} ──

    /// <summary>
    /// Lista los registros de auditoría de una entidad específica.
    /// Útil para ver el historial de cambios de un project, expense, etc.
    /// </summary>
    /// <param name="entityName">Nombre de la entidad (e.g. Project, Expense, Category).</param>
    /// <param name="entityId">ID de la entidad.</param>
    /// <response code="200">Lista de registros de auditoría.</response>
    [HttpGet("entity/{entityName}/{entityId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEntity(
        string entityName,
        Guid entityId,
        CancellationToken ct)
    {
        var logs = await _auditLogService.GetByEntityAsync(entityName, entityId, ct);
        return Ok(logs.ToResponse());
    }
}
