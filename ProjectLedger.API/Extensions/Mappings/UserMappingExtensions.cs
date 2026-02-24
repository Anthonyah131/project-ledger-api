using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.User;
using ProjectLedger.API.DTOs.Auth;

namespace ProjectLedger.API.Extensions.Mappings;

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
                Id = entity.Plan.PlnId,
                Name = entity.Plan.PlnName,
                Slug = entity.Plan.PlnSlug
            }
            : null!
    };

    // ── Summary (minimal, para listas/miembros) ─────────────

    public static UserSummaryResponse ToSummaryResponse(this User entity) => new()
    {
        Id = entity.UsrId,
        Email = entity.UsrEmail,
        FullName = entity.UsrFullName,
        AvatarUrl = entity.UsrAvatarUrl
    };

    // ── Auth info (para AuthResponse) ───────────────────────

    public static UserAuthInfo ToAuthInfo(this User entity) => new()
    {
        Id = entity.UsrId,
        Email = entity.UsrEmail,
        FullName = entity.UsrFullName,
        IsAdmin = entity.UsrIsAdmin,
        AvatarUrl = entity.UsrAvatarUrl
    };

    // ── Apply update from DTO ──────────────────────────────

    public static void ApplyUpdate(this User entity, UpdateProfileRequest request)
    {
        entity.UsrFullName = request.FullName;
        entity.UsrAvatarUrl = request.AvatarUrl;
        entity.UsrUpdatedAt = DateTime.UtcNow;
    }

    // ── Collection helpers ──────────────────────────────────

    public static IEnumerable<UserSummaryResponse> ToSummaryResponse(this IEnumerable<User> entities)
        => entities.Select(e => e.ToSummaryResponse());
}
