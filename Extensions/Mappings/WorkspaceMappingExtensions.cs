using ProjectLedger.API.DTOs.Workspace;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Extensions.Mappings;

public static class WorkspaceMappingExtensions
{
    public static Workspace ToEntity(this CreateWorkspaceRequest request, Guid ownerUserId)
        => new()
        {
            WksOwnerUserId = ownerUserId,
            WksName = request.Name,
            WksDescription = request.Description,
            WksColor = request.Color,
            WksIcon = request.Icon
        };

    public static void ApplyUpdate(this Workspace workspace, UpdateWorkspaceRequest request)
    {
        workspace.WksName = request.Name;
        workspace.WksDescription = request.Description;
        workspace.WksColor = request.Color;
        workspace.WksIcon = request.Icon;
    }

    public static WorkspaceResponse ToResponse(this Workspace workspace, string role, int projectCount)
        => new()
        {
            Id = workspace.WksId,
            Name = workspace.WksName,
            Description = workspace.WksDescription,
            Color = workspace.WksColor,
            Icon = workspace.WksIcon,
            Role = role,
            ProjectCount = projectCount,
            CreatedAt = workspace.WksCreatedAt,
            UpdatedAt = workspace.WksUpdatedAt
        };

    public static WorkspaceDetailResponse ToDetailResponse(this Workspace workspace, string role)
        => new()
        {
            Id = workspace.WksId,
            Name = workspace.WksName,
            Description = workspace.WksDescription,
            Color = workspace.WksColor,
            Icon = workspace.WksIcon,
            Role = role,
            CreatedAt = workspace.WksCreatedAt,
            UpdatedAt = workspace.WksUpdatedAt,
            Projects = workspace.Projects
                .Select(p => new WorkspaceProjectItem
                {
                    Id = p.PrjId,
                    Name = p.PrjName,
                    CurrencyCode = p.PrjCurrencyCode,
                    Description = p.PrjDescription,
                    CreatedAt = p.PrjCreatedAt
                })
                .OrderBy(p => p.Name)
                .ToList(),
            Members = workspace.Members
                .Select(m => new WorkspaceMemberItem
                {
                    UserId = m.WkmUserId,
                    FullName = m.User.UsrFullName,
                    Email = m.User.UsrEmail,
                    Role = m.WkmRole,
                    JoinedAt = m.WkmJoinedAt
                })
                .OrderBy(m => m.FullName)
                .ToList()
        };
}
