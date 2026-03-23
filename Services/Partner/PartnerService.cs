using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de partners. Un partner es un contacto financiero del usuario,
/// dueño de métodos de pago. El usuario solo puede gestionar sus propios partners.
/// </summary>
public class PartnerService : IPartnerService
{
    private readonly IPartnerRepository _partnerRepo;
    private readonly IPlanAuthorizationService _planAuth;

    public PartnerService(
        IPartnerRepository partnerRepo,
        IPlanAuthorizationService planAuth)
    {
        _partnerRepo = partnerRepo;
        _planAuth = planAuth;
    }

    public async Task<Partner?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var partner = await _partnerRepo.GetByIdAsync(id, ct);
        return partner is { PtrIsDeleted: false } ? partner : null;
    }

    public async Task<Partner?> GetByIdWithPaymentMethodsAsync(Guid id, CancellationToken ct = default)
        => await _partnerRepo.GetByIdWithPaymentMethodsAsync(id, ct);

    public async Task<IEnumerable<Partner>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _partnerRepo.GetByOwnerUserIdAsync(userId, ct);

    public async Task<(IEnumerable<Partner> Items, int TotalCount)> SearchAsync(
        Guid userId, string? search, int skip, int take, CancellationToken ct = default)
        => await _partnerRepo.SearchByNameAsync(userId, search, skip, take, ct);

    public async Task<Partner> CreateAsync(Partner partner, CancellationToken ct = default)
    {
        partner.PtrCreatedAt = DateTime.UtcNow;
        partner.PtrUpdatedAt = DateTime.UtcNow;

        await _partnerRepo.AddAsync(partner, ct);
        await _partnerRepo.SaveChangesAsync(ct);

        return partner;
    }

    public async Task UpdateAsync(Partner partner, CancellationToken ct = default)
    {
        partner.PtrUpdatedAt = DateTime.UtcNow;
        _partnerRepo.Update(partner);
        await _partnerRepo.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var partner = await _partnerRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("PartnerNotFound");

        if (partner.PtrIsDeleted)
            throw new KeyNotFoundException("PartnerNotFound");

        if (await _partnerRepo.HasActivePaymentMethodsAsync(id, ct))
            throw new InvalidOperationException("PartnerHasPaymentMethods");

        if (await _partnerRepo.IsAssignedToAnyProjectAsync(id, ct))
            throw new InvalidOperationException("PartnerHasProjects");

        partner.PtrIsDeleted = true;
        partner.PtrDeletedAt = DateTime.UtcNow;
        partner.PtrDeletedByUserId = deletedByUserId;
        partner.PtrUpdatedAt = DateTime.UtcNow;

        _partnerRepo.Update(partner);
        await _partnerRepo.SaveChangesAsync(ct);
    }

    public async Task<(IEnumerable<PaymentMethod> Items, int TotalCount)> GetPaymentMethodsPagedAsync(
        Guid partnerId, int skip, int take, CancellationToken ct = default)
        => await _partnerRepo.GetPaymentMethodsByPartnerIdPagedAsync(partnerId, skip, take, ct);

    public async Task<(IEnumerable<Project> Items, int TotalCount)> GetProjectsPagedAsync(
        Guid partnerId, int skip, int take, CancellationToken ct = default)
        => await _partnerRepo.GetProjectsByPartnerIdPagedAsync(partnerId, skip, take, ct);
}
