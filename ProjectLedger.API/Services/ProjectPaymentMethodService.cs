using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio para vincular/desvincular métodos de pago a proyectos.
/// Permite que miembros de un proyecto compartido usen los métodos vinculados.
/// </summary>
public class ProjectPaymentMethodService : IProjectPaymentMethodService
{
    private readonly IProjectPaymentMethodRepository _ppmRepo;
    private readonly IPaymentMethodRepository _pmRepo;

    public ProjectPaymentMethodService(
        IProjectPaymentMethodRepository ppmRepo,
        IPaymentMethodRepository pmRepo)
    {
        _ppmRepo = ppmRepo;
        _pmRepo = pmRepo;
    }

    public async Task<IEnumerable<ProjectPaymentMethod>> GetByProjectIdAsync(
        Guid projectId, CancellationToken ct = default)
        => await _ppmRepo.GetByProjectIdAsync(projectId, ct);

    public async Task<ProjectPaymentMethod> LinkAsync(
        ProjectPaymentMethod link, CancellationToken ct = default)
    {
        // Verificar que el método de pago existe y no está eliminado
        var pm = await _pmRepo.GetByIdAsync(link.PpmPaymentMethodId, ct)
            ?? throw new KeyNotFoundException(
                $"Payment method '{link.PpmPaymentMethodId}' not found.");

        if (pm.PmtIsDeleted)
            throw new KeyNotFoundException(
                $"Payment method '{link.PpmPaymentMethodId}' not found.");

        // Verificar que no esté ya vinculado
        var existing = await _ppmRepo.GetByProjectAndPaymentMethodAsync(
            link.PpmProjectId, link.PpmPaymentMethodId, ct);

        if (existing is not null)
            throw new InvalidOperationException(
                "This payment method is already linked to the project.");

        link.PpmCreatedAt = DateTime.UtcNow;

        await _ppmRepo.AddAsync(link, ct);
        await _ppmRepo.SaveChangesAsync(ct);

        return link;
    }

    public async Task UnlinkAsync(
        Guid projectId, Guid paymentMethodId, CancellationToken ct = default)
    {
        var link = await _ppmRepo.GetByProjectAndPaymentMethodAsync(projectId, paymentMethodId, ct)
            ?? throw new KeyNotFoundException(
                "Payment method is not linked to this project.");

        _ppmRepo.Remove(link);
        await _ppmRepo.SaveChangesAsync(ct);
    }

    public async Task<bool> IsLinkedAsync(
        Guid projectId, Guid paymentMethodId, CancellationToken ct = default)
        => await _ppmRepo.IsPaymentMethodLinkedToProjectAsync(projectId, paymentMethodId, ct);
}
