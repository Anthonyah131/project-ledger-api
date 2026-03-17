using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de partners asignados a proyectos.
/// Los métodos de pago disponibles se derivan automáticamente de los partners asignados.
/// </summary>
public class ProjectPartnerService : IProjectPartnerService
{
    private readonly IProjectPartnerRepository _repo;
    private readonly IPartnerRepository _partnerRepo;

    public ProjectPartnerService(
        IProjectPartnerRepository repo,
        IPartnerRepository partnerRepo)
    {
        _repo = repo;
        _partnerRepo = partnerRepo;
    }

    public async Task<IEnumerable<ProjectPartner>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await _repo.GetByProjectIdAsync(projectId, ct);

    public async Task<ProjectPartner> AddAsync(
        Guid projectId, Guid partnerId, Guid addedByUserId, CancellationToken ct = default)
    {
        // Verificar que el partner existe y pertenece al usuario
        var partner = await _partnerRepo.GetByIdAsync(partnerId, ct)
            ?? throw new KeyNotFoundException($"Partner '{partnerId}' not found.");

        if (partner.PtrOwnerUserId != addedByUserId)
            throw new UnauthorizedAccessException("You can only assign your own partners to projects.");

        // Verificar que no esté ya asignado
        var existing = await _repo.GetActiveAsync(projectId, partnerId, ct);
        if (existing is not null)
            throw new InvalidOperationException("This partner is already assigned to the project.");

        var projectPartner = new ProjectPartner
        {
            PtpProjectId = projectId,
            PtpPartnerId = partnerId,
            PtpAddedByUserId = addedByUserId,
            PtpCreatedAt = DateTime.UtcNow,
            PtpUpdatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(projectPartner, ct);
        await _repo.SaveChangesAsync(ct);

        // Cargar el navigation property para que el controller pueda retornar el nombre del partner
        projectPartner.Partner = partner;

        return projectPartner;
    }

    public async Task RemoveAsync(
        Guid projectId, Guid partnerId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var assignment = await _repo.GetActiveAsync(projectId, partnerId, ct)
            ?? throw new KeyNotFoundException("Partner is not assigned to this project.");

        var hasLinkedMethods = await _repo.HasPartnerPaymentMethodsLinkedToProjectAsync(projectId, partnerId, ct);
        if (hasLinkedMethods)
            throw new InvalidOperationException(
                "Cannot remove partner from project while they have payment methods linked to it. Unlink those payment methods first.");

        assignment.PtpIsDeleted = true;
        assignment.PtpDeletedAt = DateTime.UtcNow;
        assignment.PtpDeletedByUserId = deletedByUserId;
        assignment.PtpUpdatedAt = DateTime.UtcNow;

        _repo.Update(assignment);
        await _repo.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<PaymentMethod>> GetAvailablePaymentMethodsAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => await _repo.GetAvailablePaymentMethodsAsync(projectId, userId, ct);
}
