using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.Auth;
using ProjectLedger.API.DTOs.Plan;
using ProjectLedger.API.DTOs.User;

namespace ProjectLedger.API.Extensions.Mappings;

/// <summary>
/// Mapping extensions for User entity-to-DTO conversions (profile, summary, auth info).
/// </summary>
public static class UserMappingExtensions
{
    // ── Profile (full) ──────────────────────────────────────

    public static UserProfileResponse ToProfileResponse(this User entity) => new()
    {
        Id = entity.UsrId,
        Email = entity.UsrEmail,
        FullName = entity.UsrFullName,
        AvatarUrl = entity.UsrAvatarUrl,
        IsActive = entity.UsrIsActive,
        IsAdmin = entity.UsrIsAdmin,
        LastLoginAt = entity.UsrLastLoginAt,
        CreatedAt = entity.UsrCreatedAt,
        Plan = entity.Plan is not null
            ? new UserPlanSummaryDto
            {
                Id   = entity.Plan.PlnId,
                Name = entity.Plan.PlnName,
                Slug = entity.Plan.PlnSlug,
                Permissions = new PlanPermissionsDto
                {
                    CanCreateProjects       = entity.Plan.PlnCanCreateProjects,
                    CanEditProjects         = entity.Plan.PlnCanEditProjects,
                    CanDeleteProjects       = entity.Plan.PlnCanDeleteProjects,
                    CanShareProjects        = entity.Plan.PlnCanShareProjects,
                    CanExportData           = entity.Plan.PlnCanExportData,
                    CanUseAdvancedReports   = entity.Plan.PlnCanUseAdvancedReports,
                    CanUseOcr               = entity.Plan.PlnCanUseOcr,
                    CanUseApi               = entity.Plan.PlnCanUseApi,
                    CanUseMultiCurrency     = entity.Plan.PlnCanUseMultiCurrency,
                    CanSetBudgets           = entity.Plan.PlnCanSetBudgets,
                    CanUsePartners          = entity.Plan.PlnCanUsePartners
                }
            }
            : null!
    };

    // ── Summary (minimal, for lists/members) ─────────────

    public static UserSummaryResponse ToSummaryResponse(this User entity) => new()
    {
        Id = entity.UsrId,
        Email = entity.UsrEmail,
        FullName = entity.UsrFullName,
        AvatarUrl = entity.UsrAvatarUrl
    };

    // ── Auth info (for AuthResponse) ───────────────────────

    public static UserAuthInfo ToAuthInfo(this User entity) => new()
    {
        Id = entity.UsrId,
        Email = entity.UsrEmail,
        FullName = entity.UsrFullName,
        IsActive = entity.UsrIsActive,
        IsAdmin = entity.UsrIsAdmin,
        AvatarUrl = entity.UsrAvatarUrl
    };

    // ── Apply update from DTO ──────────────────────────────

    public static void ApplyUpdate(this User entity, UpdateProfileRequest request)
    {
        entity.UsrFullName = request.FullName;

        // Distinguish omitted field vs explicit null to support avatar removal.
        if (request.AvatarUrlSpecified)
            entity.UsrAvatarUrl = request.AvatarUrl;

        entity.UsrUpdatedAt = DateTime.UtcNow;
    }

    // ── Collection helpers ──────────────────────────────────

    public static IEnumerable<UserSummaryResponse> ToSummaryResponse(this IEnumerable<User> entities)
        => entities.Select(e => e.ToSummaryResponse());
}
