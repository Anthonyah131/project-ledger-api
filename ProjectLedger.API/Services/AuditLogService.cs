using System.Text.Json;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de auditoría. Registra acciones sobre entidades.
/// AuditLog es inmutable — no se edita ni se borra.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepo;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public AuditLogService(IAuditLogRepository auditLogRepo)
    {
        _auditLogRepo = auditLogRepo;
    }

    public async Task LogAsync(
        string entityName,
        Guid entityId,
        string actionType,
        Guid performedByUserId,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken ct = default)
    {
        var entry = new AuditLog
        {
            AudId = Guid.NewGuid(),
            AudEntityName = entityName,
            AudEntityId = entityId,
            AudActionType = actionType,
            AudPerformedByUserId = performedByUserId,
            AudPerformedAt = DateTime.UtcNow,
            AudOldValues = oldValues is not null
                ? JsonSerializer.Serialize(oldValues, JsonOptions)
                : null,
            AudNewValues = newValues is not null
                ? JsonSerializer.Serialize(newValues, JsonOptions)
                : null
        };

        await _auditLogRepo.AddAsync(entry, ct);
        await _auditLogRepo.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<AuditLog>> GetByEntityAsync(
        string entityName, Guid entityId, CancellationToken ct = default)
        => await _auditLogRepo.GetByEntityAsync(entityName, entityId, ct);

    public async Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _auditLogRepo.GetByUserIdAsync(userId, ct);
}
