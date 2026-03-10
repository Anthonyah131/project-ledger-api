using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.Admin;

namespace ProjectLedger.API.Extensions.Mappings;

public static class AdminMappingExtensions
{
    public static AdminUserResponse ToAdminResponse(this User entity) => new()
    {
        Id = entity.UsrId,
        Email = entity.UsrEmail,
        FullName = entity.UsrFullName,
        AvatarUrl = entity.UsrAvatarUrl,
        IsActive = entity.UsrIsActive,
        IsAdmin = entity.UsrIsAdmin,
        IsDeleted = entity.UsrIsDeleted,
        LastLoginAt = entity.UsrLastLoginAt,
        CreatedAt = entity.UsrCreatedAt,
        UpdatedAt = entity.UsrUpdatedAt,
        Plan = entity.Plan is not null
            ? new AdminUserPlanDto
            {
                Id = entity.Plan.PlnId,
                Name = entity.Plan.PlnName,
                Slug = entity.Plan.PlnSlug
            }
            : null
    };

    public static void ApplyAdminUpdate(this User entity, AdminUpdateUserRequest request)
    {
        entity.UsrFullName = request.FullName;
        entity.UsrAvatarUrl = request.AvatarUrl;
        if (request.PlanId.HasValue)
            entity.UsrPlanId = request.PlanId.Value;
        if (request.IsAdmin.HasValue)
            entity.UsrIsAdmin = request.IsAdmin.Value;
        entity.UsrUpdatedAt = DateTime.UtcNow;
    }
}
