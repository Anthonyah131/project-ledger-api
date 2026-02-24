using System.Text.Json;
using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.Plan;

namespace ProjectLedger.API.Extensions.Mappings;

public static class PlanMappingExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static PlanResponse ToResponse(this Plan entity) => new()
    {
        Id = entity.PlnId,
        Name = entity.PlnName,
        Slug = entity.PlnSlug,
        Description = entity.PlnDescription,
        DisplayOrder = entity.PlnDisplayOrder,
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
        Limits = string.IsNullOrWhiteSpace(entity.PlnLimits)
            ? null
            : JsonSerializer.Deserialize<PlanLimitsDto>(entity.PlnLimits, JsonOptions)
    };

    public static IEnumerable<PlanResponse> ToResponse(this IEnumerable<Plan> entities)
        => entities.Select(e => e.ToResponse());
}
