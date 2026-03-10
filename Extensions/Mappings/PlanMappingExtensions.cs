using System.Text.Json;
using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.Plan;

namespace ProjectLedger.API.Extensions.Mappings;

public static class PlanMappingExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static PlanResponse ToResponse(this Plan entity) => new()
    {
        Id = entity.PlnId,
        Name = entity.PlnName,
        Slug = entity.PlnSlug,
        Description = entity.PlnDescription,
        DisplayOrder = entity.PlnDisplayOrder,
        MonthlyPrice = entity.PlnMonthlyPrice,
        Currency = entity.PlnCurrency,
        StripePaymentLinkUrl = entity.PlnStripePaymentLinkUrl,
        Permissions = new PlanPermissionsDto
        {
            CanCreateProjects = entity.PlnCanCreateProjects,
            CanEditProjects = entity.PlnCanEditProjects,
            CanDeleteProjects = entity.PlnCanDeleteProjects,
            CanShareProjects = entity.PlnCanShareProjects,
            CanExportData = entity.PlnCanExportData,
            CanUseAdvancedReports = entity.PlnCanUseAdvancedReports,
            CanUseOcr = entity.PlnCanUseOcr,
            CanUseApi = entity.PlnCanUseApi,
            CanUseMultiCurrency = entity.PlnCanUseMultiCurrency,
            CanSetBudgets = entity.PlnCanSetBudgets
        },
        Limits = DeserializeLimits(entity.PlnLimits)
    };

    public static IEnumerable<PlanResponse> ToResponse(this IEnumerable<Plan> entities)
        => entities.Select(e => e.ToResponse());

    private static PlanLimitsDto? DeserializeLimits(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var limits = JsonSerializer.Deserialize<PlanLimitsDto>(json, JsonOptions);
            if (limits is null)
                return null;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

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
