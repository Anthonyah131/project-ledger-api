using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.DTOs.Plan;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// IPlanAuthorizationService implementation.
/// 
/// Workflow:
/// 1. Retrieves User with their Plan (Include) from repository.
/// 2. Reads the corresponding boolean permission from the Plan.
/// 3. For limits → deserializes PlnLimits (JSONB) and compares against current count.
/// </summary>
public class PlanAuthorizationService : IPlanAuthorizationService
{
    private readonly IUserRepository _userRepo;
    private readonly IPlanRepository _planRepo;
    private readonly IProjectRepository _projectRepo;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public PlanAuthorizationService(
        IUserRepository userRepo,
        IPlanRepository planRepo,
        IProjectRepository projectRepo)
    {
        _userRepo = userRepo;
        _planRepo = planRepo;
        _projectRepo = projectRepo;
    }

    // ── Boolean Permissions ─────────────────────────────────

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

    // ── Numeric Limits ──────────────────────────────────────

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

    // ── Complete Plan Load ──────────────────────────────────

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
                [nameof(PlanPermission.CanSetBudgets)]         = plan.PlnCanSetBudgets,
                [nameof(PlanPermission.CanUsePartners)]        = plan.PlnCanUsePartners
            },
            Limits = new Dictionary<string, int?>
            {
                [PlanLimits.MaxProjects]              = limits?.MaxProjects,
                [PlanLimits.MaxExpensesPerMonth]      = limits?.MaxExpensesPerMonth,
                [PlanLimits.MaxCategoriesPerProject]  = limits?.MaxCategoriesPerProject,
                [PlanLimits.MaxPaymentMethods]        = limits?.MaxPaymentMethods,
                [PlanLimits.MaxTeamMembersPerProject] = limits?.MaxTeamMembersPerProject,
                [PlanLimits.MaxAlternativeCurrenciesPerProject] = limits?.MaxAlternativeCurrenciesPerProject,
                [PlanLimits.MaxIncomesPerMonth]       = limits?.MaxIncomesPerMonth,
                [PlanLimits.MaxDocumentReadsPerMonth] = limits?.MaxDocumentReadsPerMonth
            }
        };
    }

    // ── Project Write Access Validation ─────────────────────

    /// <inheritdoc />
    public async Task ValidateProjectWriteAccessAsync(
        Guid projectId, Guid actingUserId, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        if (project.PrjIsDeleted)
            throw new KeyNotFoundException("ProjectNotFound");

        // The OWNER's plan governs the entire project
        var ownerPlan = await GetUserPlanAsync(project.PrjOwnerUserId, ct);

        // 1. El plan del owner debe permitir ediciones
        if (!EvaluatePermission(ownerPlan, PlanPermission.CanEditProjects))
            throw new PlanDeniedException(PlanPermission.CanEditProjects, ownerPlan.PlnName);

        // 2. If the actor is NOT the owner → they are a shared member.
        //    The owner's plan must allow project sharing.
        if (actingUserId != project.PrjOwnerUserId)
        {
            if (!EvaluatePermission(ownerPlan, PlanPermission.CanShareProjects))
                throw new PlanDeniedException("PlanDenied");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Private Helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>Gets the User's Plan. Throws if not found.</summary>
    private async Task<Plan> GetUserPlanAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);

        if (user is null || user.UsrIsDeleted)
            throw new UnauthorizedAccessException("UserNotFoundOrDeleted");

        var plan = await _planRepo.GetByIdAsync(user.UsrPlanId, ct);

        if (plan is null || !plan.PlnIsActive)
            throw new InvalidOperationException("PlanNotFoundOrInactive");

        return plan;
    }

    /// <summary>Evaluates a boolean permission from the plan.</summary>
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
        PlanPermission.CanUsePartners        => plan.PlnCanUsePartners,
        _ => false
    };

    /// <summary>Gets a limit value from the plan's JSONB by name.</summary>
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
            PlanLimits.MaxAlternativeCurrenciesPerProject => limits.MaxAlternativeCurrenciesPerProject,
            PlanLimits.MaxIncomesPerMonth       => limits.MaxIncomesPerMonth,
            PlanLimits.MaxDocumentReadsPerMonth => limits.MaxDocumentReadsPerMonth,
            _ => null
        };
    }

    /// <summary>Deserializes the plan's limits JSONB.</summary>
    private static PlanLimitsDto? DeserializeLimits(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var limits = JsonSerializer.Deserialize<PlanLimitsDto>(json, JsonOptions);
            if (limits is null)
                return null;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Compatibility with legacy datasets.
            if (limits.MaxExpensesPerMonth is null
                && TryGetNullableInt(root, "max_expenses", out var legacyMaxExpenses))
            {
                limits.MaxExpensesPerMonth = legacyMaxExpenses;
            }

            NormalizeLegacyUnlimitedValues(limits);
            return limits;
        }
        catch
        {
            return null;
        }
    }

    private static void NormalizeLegacyUnlimitedValues(PlanLimitsDto limits)
    {
        limits.MaxProjects = NormalizeLegacyUnlimited(limits.MaxProjects);
        limits.MaxExpensesPerMonth = NormalizeLegacyUnlimited(limits.MaxExpensesPerMonth);
        limits.MaxCategoriesPerProject = NormalizeLegacyUnlimited(limits.MaxCategoriesPerProject);
        limits.MaxPaymentMethods = NormalizeLegacyUnlimited(limits.MaxPaymentMethods);
        limits.MaxTeamMembersPerProject = NormalizeLegacyUnlimited(limits.MaxTeamMembersPerProject);
        limits.MaxAlternativeCurrenciesPerProject = NormalizeLegacyUnlimited(limits.MaxAlternativeCurrenciesPerProject);
        limits.MaxIncomesPerMonth = NormalizeLegacyUnlimited(limits.MaxIncomesPerMonth);
        limits.MaxDocumentReadsPerMonth = NormalizeLegacyUnlimited(limits.MaxDocumentReadsPerMonth);
    }

    private static int? NormalizeLegacyUnlimited(int? value)
        => value is < 0 ? null : value;

    private static bool TryGetNullableInt(JsonElement root, string propertyName, out int? value)
    {
        value = null;

        if (!root.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.Null)
        {
            value = null;
            return true;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }
}
