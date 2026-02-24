using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de métodos de pago. CRUD con soft delete.
/// Valida límite de payment methods según el plan del usuario.
/// </summary>
public class PaymentMethodService : IPaymentMethodService
{
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly IPlanAuthorizationService _planAuth;

    public PaymentMethodService(
        IPaymentMethodRepository paymentMethodRepo,
        IPlanAuthorizationService planAuth)
    {
        _paymentMethodRepo = paymentMethodRepo;
        _planAuth = planAuth;
    }

    public async Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var pm = await _paymentMethodRepo.GetByIdAsync(id, ct);
        return pm is { PmtIsDeleted: false } ? pm : null;
    }

    public async Task<IEnumerable<PaymentMethod>> GetByOwnerUserIdAsync(
        Guid userId, CancellationToken ct = default)
        => await _paymentMethodRepo.GetByOwnerUserIdAsync(userId, ct);

    public async Task<PaymentMethod> CreateAsync(PaymentMethod paymentMethod, CancellationToken ct = default)
    {
        // Validar límite de payment methods del plan del usuario
        var existing = await _paymentMethodRepo.GetByOwnerUserIdAsync(paymentMethod.PmtOwnerUserId, ct);
        await _planAuth.ValidateLimitAsync(
            paymentMethod.PmtOwnerUserId, PlanLimits.MaxPaymentMethods, existing.Count(), ct);

        paymentMethod.PmtCreatedAt = DateTime.UtcNow;
        paymentMethod.PmtUpdatedAt = DateTime.UtcNow;

        await _paymentMethodRepo.AddAsync(paymentMethod, ct);
        await _paymentMethodRepo.SaveChangesAsync(ct);

        return paymentMethod;
    }

    public async Task UpdateAsync(PaymentMethod paymentMethod, CancellationToken ct = default)
    {
        paymentMethod.PmtUpdatedAt = DateTime.UtcNow;
        _paymentMethodRepo.Update(paymentMethod);
        await _paymentMethodRepo.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var pm = await _paymentMethodRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Payment method '{id}' not found.");

        if (pm.PmtIsDeleted)
            throw new KeyNotFoundException($"Payment method '{id}' not found.");

        pm.PmtIsDeleted = true;
        pm.PmtDeletedAt = DateTime.UtcNow;
        pm.PmtDeletedByUserId = deletedByUserId;
        pm.PmtUpdatedAt = DateTime.UtcNow;

        _paymentMethodRepo.Update(pm);
        await _paymentMethodRepo.SaveChangesAsync(ct);
    }
}
