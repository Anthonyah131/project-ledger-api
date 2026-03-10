using System.Text.Json;
using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.AuditLog;

namespace ProjectLedger.API.Extensions.Mappings;

public static class AuditLogMappingExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── Entity → Response ───────────────────────────────────

    public static AuditLogResponse ToResponse(this AuditLog entity) => new()
    {
        Id = entity.AudId,
        EntityName = entity.AudEntityName,
        EntityId = entity.AudEntityId,
        ActionType = entity.AudActionType,
        PerformedByUserId = entity.AudPerformedByUserId,
        PerformedByUserName = entity.PerformedByUser?.UsrFullName,
        PerformedAt = entity.AudPerformedAt,
        OldValues = DeserializeJson(entity.AudOldValues),
        NewValues = DeserializeJson(entity.AudNewValues)
    };

    // ── Factory (para crear AuditLog desde el servicio) ─────

    public static AuditLog Create(
        string entityName,
        Guid entityId,
        string actionType,
        Guid performedByUserId,
        object? oldValues = null,
        object? newValues = null) => new()
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

    // ── Collection helpers ──────────────────────────────────

    public static IEnumerable<AuditLogResponse> ToResponse(this IEnumerable<AuditLog> entities)
        => entities.Select(e => e.ToResponse());

    // ── Private ─────────────────────────────────────────────

    private static object? DeserializeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        }
        catch
        {
            return json; // fallback: devolver el string crudo
        }
    }
}
