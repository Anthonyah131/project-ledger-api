using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.DTOs.Plan;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Implementación de IPlanAuthorizationService.
/// 
/// Flujo:
/// 1. Obtiene el User con su Plan (Include) desde el repositorio
/// 2. Lee el permiso booleano correspondiente del Plan
/// 3. Para límites → deserializa PlnLimits (JSONB) y compara contra el count actual
/// </summary>
public class PlanAuthorizationService : IPlanAuthorizationService
{
    private readonly IUserRepository _userRepo;
    private readonly IPlanRepository _planRepo;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PlanAuthorizationService(IUserRepository userRepo, IPlanRepository planRepo)
    {
        _userRepo = userRepo;
        _planRepo = planRepo;
    }

    // ── Permisos booleanos ──────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(
        Guid userId, PlanPermission permission, CancellationToken ct = default)
    {
        var plan = await GetUserPlanAsync(userId, ct);
        return EvaluatePermission(plan, permission);
    }

    /// <inheritdoc />
    public async Task ValidatePermissionAsync(
        Guid userId, PlanPermission permission, CancellationToken ct = default)
    {
        var plan = await GetUserPlanAsync(userId, ct);

        if (!EvaluatePermission(plan, permission))
            throw new PlanDeniedException(permission, plan.PlnName);
    }

    // ── Límites numéricos ───────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> IsWithinLimitAsync(
        Guid userId, string limitName, int currentCount, CancellationToken ct = default)
    {
        var plan = await GetUserPlanAsync(userId, ct);
        var limit = GetLimitValue(plan, limitName);

        // null = ilimitado
        return limit is null || currentCount < limit.Value;
    }

    /// <inheritdoc />
    public async Task ValidateLimitAsync(
        Guid userId, string limitName, int currentCount, CancellationToken ct = default)
    {
        var plan = await GetUserPlanAsync(userId, ct);
        var limit = GetLimitValue(plan, limitName);

        if (limit is not null && currentCount >= limit.Value)
            throw new PlanLimitExceededException(limitName, limit.Value, plan.PlnName);
    }

    // ── Carga completa del plan ─────────────────────────────

    /// <inheritdoc />
    public async Task<PlanCapabilities> GetCapabilitiesAsync(
        Guid userId, CancellationToken ct = default)
    {
        var plan = await GetUserPlanAsync(userId, ct);
        var limits = DeserializeLimits(plan.PlnLimits);

        return new PlanCapabilities
        {
            PlanName = plan.PlnName,
            PlanSlug = plan.PlnSlug,
            Permissions = new Dictionary<string, bool>
            {
                [nameof(PlanPermission.CanCreateProjects)]     = plan.PlnCanCreateProjects,
                [nameof(PlanPermission.CanEditProjects)]       = plan.PlnCanEditProjects,
                [nameof(PlanPermission.CanDeleteProjects)]     = plan.PlnCanDeleteProjects,
                [nameof(PlanPermission.CanShareProjects)]      = plan.PlnCanShareProjects,
                [nameof(PlanPermission.CanExportData)]         = plan.PlnCanExportData,
                [nameof(PlanPermission.CanUseAdvancedReports)] = plan.PlnCanUseAdvancedReports,
                [nameof(PlanPermission.CanUseOcr)]             = plan.PlnCanUseOcr,
                [nameof(PlanPermission.CanUseApi)]             = plan.PlnCanUseApi,
                [nameof(PlanPermission.CanUseMultiCurrency)]   = plan.PlnCanUseMultiCurrency,
                [nameof(PlanPermission.CanSetBudgets)]         = plan.PlnCanSetBudgets
            },
            Limits = new Dictionary<string, int?>
            {
                [PlanLimits.MaxProjects]              = limits?.MaxProjects,
                [PlanLimits.MaxExpensesPerMonth]      = limits?.MaxExpensesPerMonth,
                [PlanLimits.MaxCategoriesPerProject]  = limits?.MaxCategoriesPerProject,
                [PlanLimits.MaxPaymentMethods]        = limits?.MaxPaymentMethods,
                [PlanLimits.MaxTeamMembersPerProject] = limits?.MaxTeamMembersPerProject
            }
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  Private Helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>Obtiene el Plan del usuario. Lanza si no existe.</summary>
    private async Task<Plan> GetUserPlanAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);

        if (user is null || user.UsrIsDeleted)
            throw new UnauthorizedAccessException("User not found.");

        var plan = await _planRepo.GetByIdAsync(user.UsrPlanId, ct);

        if (plan is null || !plan.PlnIsActive)
            throw new InvalidOperationException(
                $"Plan '{user.UsrPlanId}' not found or inactive for user '{userId}'.");

        return plan;
    }

    /// <summary>Evalúa un permiso booleano del plan.</summary>
    private static bool EvaluatePermission(Plan plan, PlanPermission permission) => permission switch
    {
        PlanPermission.CanCreateProjects     => plan.PlnCanCreateProjects,
        PlanPermission.CanEditProjects       => plan.PlnCanEditProjects,
        PlanPermission.CanDeleteProjects     => plan.PlnCanDeleteProjects,
        PlanPermission.CanShareProjects      => plan.PlnCanShareProjects,
        PlanPermission.CanExportData         => plan.PlnCanExportData,
        PlanPermission.CanUseAdvancedReports => plan.PlnCanUseAdvancedReports,
        PlanPermission.CanUseOcr             => plan.PlnCanUseOcr,
        PlanPermission.CanUseApi             => plan.PlnCanUseApi,
        PlanPermission.CanUseMultiCurrency   => plan.PlnCanUseMultiCurrency,
        PlanPermission.CanSetBudgets         => plan.PlnCanSetBudgets,
        _ => false
    };

    /// <summary>Obtiene un valor de límite del JSONB del plan por nombre.</summary>
    private static int? GetLimitValue(Plan plan, string limitName)
    {
        var limits = DeserializeLimits(plan.PlnLimits);
        if (limits is null) return null; // Sin limits definidos → ilimitado

        return limitName switch
        {
            PlanLimits.MaxProjects              => limits.MaxProjects,
            PlanLimits.MaxExpensesPerMonth      => limits.MaxExpensesPerMonth,
            PlanLimits.MaxCategoriesPerProject  => limits.MaxCategoriesPerProject,
            PlanLimits.MaxPaymentMethods        => limits.MaxPaymentMethods,
            PlanLimits.MaxTeamMembersPerProject => limits.MaxTeamMembersPerProject,
            _ => null
        };
    }

    /// <summary>Deserializa el JSONB de límites del plan.</summary>
    private static PlanLimitsDto? DeserializeLimits(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<PlanLimitsDto>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
