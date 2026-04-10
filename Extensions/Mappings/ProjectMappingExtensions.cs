using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.Project;

namespace ProjectLedger.API.Extensions.Mappings;

/// <summary>
/// Mapping extensions for Project and ProjectMember entity-to-DTO conversions.
/// </summary>
public static class ProjectMappingExtensions
{
    // ── Entity → Response ───────────────────────────────────

    public static ProjectResponse ToResponse(this Project entity, string userRole) => new()
    {
        Id = entity.PrjId,
        Name = entity.PrjName,
        CurrencyCode = entity.PrjCurrencyCode,
        Description = entity.PrjDescription,
        OwnerUserId = entity.PrjOwnerUserId,
        UserRole = userRole,
        WorkspaceId = entity.PrjWorkspaceId,
        WorkspaceName = entity.Workspace?.WksName,
        PartnersEnabled = entity.PrjPartnersEnabled,
        CreatedAt = entity.PrjCreatedAt,
        UpdatedAt = entity.PrjUpdatedAt
    };

    public static PinnedProjectResponse ToPinnedResponse(this Project entity, string userRole, DateTime pinnedAt) => new()
    {
        Id = entity.PrjId,
        Name = entity.PrjName,
        CurrencyCode = entity.PrjCurrencyCode,
        Description = entity.PrjDescription,
        OwnerUserId = entity.PrjOwnerUserId,
        UserRole = userRole,
        WorkspaceId = entity.PrjWorkspaceId,
        WorkspaceName = entity.Workspace?.WksName,
        PartnersEnabled = entity.PrjPartnersEnabled,
        CreatedAt = entity.PrjCreatedAt,
        UpdatedAt = entity.PrjUpdatedAt,
        PinnedAt = pinnedAt
    };

    // ── Request → Entity ────────────────────────────────────

    public static Project ToEntity(this CreateProjectRequest request, Guid ownerUserId) => new()
    {
        PrjId = Guid.NewGuid(),
        PrjName = request.Name,
        PrjCurrencyCode = request.CurrencyCode,
        PrjDescription = request.Description,
        PrjOwnerUserId = ownerUserId,
        PrjCreatedAt = DateTime.UtcNow,
        PrjUpdatedAt = DateTime.UtcNow
        // PrjWorkspaceId is resolved and set by the controller
    };

    // ── Apply update from DTO ──────────────────────────────

    public static void ApplyUpdate(this Project entity, UpdateProjectRequest request)
    {
        entity.PrjName = request.Name;
        entity.PrjDescription = request.Description;
        entity.PrjUpdatedAt = DateTime.UtcNow;
    }

    // ── Member mapping ──────────────────────────────────────

    public static ProjectMemberResponse ToResponse(this ProjectMember entity) => new()
    {
        Id = entity.PrmId,
        UserId = entity.PrmUserId,
        UserFullName = entity.User?.UsrFullName ?? string.Empty,
        UserEmail = entity.User?.UsrEmail ?? string.Empty,
        Role = entity.PrmRole,
        JoinedAt = entity.PrmJoinedAt
    };

    public static IEnumerable<ProjectMemberResponse> ToResponse(this IEnumerable<ProjectMember> entities)
        => entities.Select(e => e.ToResponse());
}
