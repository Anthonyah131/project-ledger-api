using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.AuditLog;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Audit log controller. Read-only.
/// 
/// Only accessible by the authenticated user for their own actions,
/// or by admins (per entity).
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
    /// Lists the audit records of the authenticated user's actions (paginated).
    /// Always ordered by descending date (most recent first).
    /// SortBy and SortDirection are ignored — chronological order is fixed.
    /// </summary>
    /// <response code="200">Paginated list of audit records.</response>
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
    /// Lists the audit records of a specific entity (paginated).
    /// Useful for viewing the change history of a project, expense, etc.
    /// Always ordered by descending date. SortBy/SortDirection are ignored.
    /// </summary>
    /// <param name="entityName">Entity name (e.g., Project, Expense, Category).</param>
    /// <param name="entityId">Entity ID.</param>
    /// <response code="200">Paginated list of audit records.</response>
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
