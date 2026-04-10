using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Extensions.Mappings;

/// <summary>
/// Mapping extensions for Partner entity-to-DTO conversions.
/// </summary>
public static class PartnerMappingExtensions
{
    // ── Entity → Response ───────────────────────────────────

    public static PartnerResponse ToResponse(this Partner entity) => new()
    {
        Id = entity.PtrId,
        Name = entity.PtrName,
        Email = entity.PtrEmail,
        Phone = entity.PtrPhone,
        Notes = entity.PtrNotes,
        CreatedAt = entity.PtrCreatedAt,
        UpdatedAt = entity.PtrUpdatedAt
    };

    public static PartnerDetailResponse ToDetailResponse(this Partner entity) => new()
    {
        Id = entity.PtrId,
        Name = entity.PtrName,
        Email = entity.PtrEmail,
        Phone = entity.PtrPhone,
        Notes = entity.PtrNotes,
        CreatedAt = entity.PtrCreatedAt,
        UpdatedAt = entity.PtrUpdatedAt,
        PaymentMethods = entity.PaymentMethods
            .Where(pm => !pm.PmtIsDeleted)
            .Select(pm => new PartnerPaymentMethodResponse
            {
                Id = pm.PmtId,
                Name = pm.PmtName,
                Type = pm.PmtType,
                Currency = pm.PmtCurrency,
                BankName = pm.PmtBankName
            })
            .ToList(),
        Projects = entity.PaymentMethods
            .Where(pm => !pm.PmtIsDeleted)
            .SelectMany(pm => pm.ProjectPaymentMethods)
            .Select(ppm => ppm.Project)
            .Where(p => p is not null && !p.PrjIsDeleted)
            .DistinctBy(p => p.PrjId)
            .Select(p => new PartnerProjectResponse
            {
                Id = p.PrjId,
                Name = p.PrjName,
                CurrencyCode = p.PrjCurrencyCode,
                Description = p.PrjDescription,
                WorkspaceId = p.PrjWorkspaceId,
                WorkspaceName = p.Workspace?.WksName
            })
            .ToList()
    };

    // ── Request → Entity ────────────────────────────────────

    public static Partner ToEntity(this CreatePartnerRequest request, Guid ownerUserId) => new()
    {
        PtrId = Guid.NewGuid(),
        PtrOwnerUserId = ownerUserId,
        PtrName = request.Name,
        PtrEmail = request.Email,
        PtrPhone = request.Phone,
        PtrNotes = request.Notes,
        PtrCreatedAt = DateTime.UtcNow,
        PtrUpdatedAt = DateTime.UtcNow
    };

    // ── Apply update from DTO ──────────────────────────────

    public static void ApplyUpdate(this Partner entity, UpdatePartnerRequest request)
    {
        entity.PtrName = request.Name;
        entity.PtrEmail = request.Email;
        entity.PtrPhone = request.Phone;
        entity.PtrNotes = request.Notes;
        entity.PtrUpdatedAt = DateTime.UtcNow;
    }

    // ── Collection helpers ──────────────────────────────────

    public static IEnumerable<PartnerResponse> ToResponse(this IEnumerable<Partner> entities)
        => entities.Select(e => e.ToResponse());
}
