using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de obligaciones/deudas. CRUD con soft delete.
/// </summary>
public class ObligationService : IObligationService
{
    private readonly IObligationRepository _obligationRepo;
    private readonly IAuditLogService _auditLog;

    public ObligationService(IObligationRepository obligationRepo, IAuditLogService auditLog)
    {
        _obligationRepo = obligationRepo;
        _auditLog = auditLog;
    }

    public async Task<Obligation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var obligation = await _obligationRepo.GetByIdAsync(id, ct);
        return obligation is { OblIsDeleted: false } ? obligation : null;
    }

    public async Task<IEnumerable<Obligation>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await _obligationRepo.GetByProjectIdAsync(projectId, ct);

    public async Task<(IReadOnlyList<Obligation> Items, int TotalCount)> GetByProjectIdPagedAsync(
        Guid projectId, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default)
        => await _obligationRepo.GetByProjectIdPagedAsync(projectId, skip, take, sortBy, descending, ct);

    public async Task<Obligation> CreateAsync(Obligation obligation, CancellationToken ct = default)
    {
        obligation.OblCreatedAt = DateTime.UtcNow;
        obligation.OblUpdatedAt = DateTime.UtcNow;

        await _obligationRepo.AddAsync(obligation, ct);
        await _obligationRepo.SaveChangesAsync(ct);

        _ = _auditLog.LogAsync("Obligation", obligation.OblId, "create", obligation.OblCreatedByUserId,
            newValues: new { obligation.OblId, obligation.OblTitle, obligation.OblTotalAmount }, ct: ct);

        return obligation;
    }

    public async Task UpdateAsync(Obligation obligation, CancellationToken ct = default)
    {
        obligation.OblUpdatedAt = DateTime.UtcNow;
        _obligationRepo.Update(obligation);
        await _obligationRepo.SaveChangesAsync(ct);

        _ = _auditLog.LogAsync("Obligation", obligation.OblId, "update", obligation.OblCreatedByUserId,
            newValues: new { obligation.OblTitle, obligation.OblTotalAmount, obligation.OblDueDate }, ct: ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var obligation = await _obligationRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Obligation '{id}' not found.");

        if (obligation.OblIsDeleted)
            throw new KeyNotFoundException($"Obligation '{id}' not found.");

        obligation.OblIsDeleted = true;
        obligation.OblDeletedAt = DateTime.UtcNow;
        obligation.OblDeletedByUserId = deletedByUserId;
        obligation.OblUpdatedAt = DateTime.UtcNow;

        _obligationRepo.Update(obligation);
        await _obligationRepo.SaveChangesAsync(ct);

        _ = _auditLog.LogAsync("Obligation", id, "delete", deletedByUserId,
            oldValues: new { obligation.OblTitle }, ct: ct);
    }
}
