
namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de verificación de permisos y límites según el plan del usuario.
/// 
/// Dos modos de uso:
/// 1. IMPERATIVO (en servicios/controllers):
///    <code>await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanExportData);</code>
///    Lanza PlanDeniedException si no tiene el permiso.
/// 
/// 2. DECLARATIVO (en controllers con Authorization Policies):
///    <code>[Authorize(Policy = "Plan:CanExportData")]</code>
///    El PlanPermissionHandler resuelve automáticamente.
/// </summary>
public interface IPlanAuthorizationService
{
    // ── Permisos booleanos ──────────────────────────────────

    /// <summary>
    /// Verifica si el plan del usuario permite la acción indicada.
    /// Retorna true/false sin lanzar excepción.
    /// </summary>
    Task<bool> HasPermissionAsync(
        Guid userId,
        PlanPermission permission,
        CancellationToken ct = default);

    /// <summary>
    /// Igual que HasPermissionAsync pero lanza PlanDeniedException si no tiene permiso.
    /// Ideal para validación imperativa en servicios.
    /// </summary>
    Task ValidatePermissionAsync(
        Guid userId,
        PlanPermission permission,
        CancellationToken ct = default);

    // ── Límites numéricos ───────────────────────────────────

    /// <summary>
    /// Verifica si el usuario puede crear más entidades del tipo indicado
    /// según los límites de su plan. Si el límite es null → ilimitado.
    /// </summary>
    Task<bool> IsWithinLimitAsync(
        Guid userId,
        string limitName,
        int currentCount,
        CancellationToken ct = default);

    /// <summary>
    /// Igual que IsWithinLimitAsync pero lanza PlanLimitExceededException si excede.
    /// </summary>
    Task ValidateLimitAsync(
        Guid userId,
        string limitName,
        int currentCount,
        CancellationToken ct = default);

    // ── Carga completa del plan ─────────────────────────────

    /// <summary>
    /// Obtiene un resumen completo de permisos y límites del plan del usuario.
    /// Útil para el frontend (mostrar qué features tiene disponibles).
    /// </summary>
    Task<PlanCapabilities> GetCapabilitiesAsync(
        Guid userId,
        CancellationToken ct = default);
}

/// <summary>
/// Resumen de las capacidades del plan de un usuario.
/// Se devuelve al frontend para mostrar/ocultar features.
/// </summary>
public class PlanCapabilities
{
    public string PlanName { get; set; } = null!;
    public string PlanSlug { get; set; } = null!;

    // ── Permisos ────────────────────────────────────────────
    public Dictionary<string, bool> Permissions { get; set; } = new();

    // ── Límites (null = ilimitado) ──────────────────────────
    public Dictionary<string, int?> Limits { get; set; } = new();
}
