using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.PaymentMethod;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;
using ProjectLedger.API.Resources;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de métodos de pago. CRUD con soft delete.
/// Valida límite de payment methods según el plan del usuario.
/// </summary>
public class PaymentMethodService : IPaymentMethodService
{
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly IPartnerRepository _partnerRepo;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly ITransactionReferenceGuardService _transactionReferenceGuard;
    private readonly IStringLocalizer<Messages> _localizer;

    public PaymentMethodService(
        IPaymentMethodRepository paymentMethodRepo,
        IPartnerRepository partnerRepo,
        IPlanAuthorizationService planAuth,
        ITransactionReferenceGuardService transactionReferenceGuard,
        IStringLocalizer<Messages> localizer)
    {
        _paymentMethodRepo = paymentMethodRepo;
        _partnerRepo = partnerRepo;
        _planAuth = planAuth;
        _transactionReferenceGuard = transactionReferenceGuard;
        _localizer = localizer;
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
            ?? throw new KeyNotFoundException(_localizer["PaymentMethodNotFound"]);

        if (pm.PmtIsDeleted)
            throw new KeyNotFoundException(_localizer["PaymentMethodNotFound"]);

        await _transactionReferenceGuard.EnsurePaymentMethodCanBeDeletedAsync(id, ct);

        pm.PmtIsDeleted = true;
        pm.PmtDeletedAt = DateTime.UtcNow;
        pm.PmtDeletedByUserId = deletedByUserId;
        pm.PmtUpdatedAt = DateTime.UtcNow;

        _paymentMethodRepo.Update(pm);
        await _paymentMethodRepo.SaveChangesAsync(ct);
    }

    public async Task<PaymentMethodBalanceResponse> GetProjectBalanceAsync(
        Guid pmtId, Guid projectId, CancellationToken ct = default)
    {
        var pm = await _paymentMethodRepo.GetByIdAsync(pmtId, ct)
            ?? throw new KeyNotFoundException(_localizer["PaymentMethodNotFound"]);

        var (totalIncome, totalExpenses) = await _paymentMethodRepo.GetProjectBalanceAsync(pmtId, projectId, ct);

        return new PaymentMethodBalanceResponse
        {
            PaymentMethodId = pm.PmtId,
            PaymentMethodName = pm.PmtName,
            Currency = pm.PmtCurrency,
            ProjectId = projectId,
            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            Balance = totalIncome - totalExpenses
        };
    }

    public async Task<PaymentMethod> LinkPartnerAsync(Guid pmtId, Guid partnerId, Guid userId, CancellationToken ct = default)
    {
        var pm = await _paymentMethodRepo.GetByIdAsync(pmtId, ct)
            ?? throw new KeyNotFoundException(_localizer["PaymentMethodNotFound"]);

        if (pm.PmtOwnerUserId != userId)
            throw new KeyNotFoundException(_localizer["PaymentMethodNotFound"]);

        var partner = await _partnerRepo.GetByIdAsync(partnerId, ct)
            ?? throw new KeyNotFoundException(_localizer["PartnerNotFound"]);

        if (partner.PtrOwnerUserId != userId)
            throw new KeyNotFoundException(_localizer["PartnerNotFound"]);

        if (pm.PmtOwnerPartnerId == partnerId)
            throw new InvalidOperationException(_localizer["PaymentMethodAlreadyLinked"]);

        pm.PmtOwnerPartnerId = partnerId;
        pm.OwnerPartner = partner;
        pm.PmtUpdatedAt = DateTime.UtcNow;

        _paymentMethodRepo.Update(pm);
        await _paymentMethodRepo.SaveChangesAsync(ct);

        return pm;
    }

    public async Task<PaymentMethod> UnlinkPartnerAsync(Guid pmtId, Guid userId, CancellationToken ct = default)
    {
        var pm = await _paymentMethodRepo.GetByIdAsync(pmtId, ct)
            ?? throw new KeyNotFoundException(_localizer["PaymentMethodNotFound"]);

        if (pm.PmtOwnerUserId != userId)
            throw new KeyNotFoundException(_localizer["PaymentMethodNotFound"]);

        if (pm.PmtOwnerPartnerId is null)
            throw new InvalidOperationException(_localizer["PaymentMethodNoPartnerLinked"]);

        var isLinkedToProject = await _paymentMethodRepo.IsLinkedToAnyProjectAsync(pmtId, ct);
        if (isLinkedToProject)
            throw new InvalidOperationException(_localizer["PaymentMethodCannotUnlinkPartner"]);

        pm.PmtOwnerPartnerId = null;
        pm.OwnerPartner = null;
        pm.PmtUpdatedAt = DateTime.UtcNow;

        _paymentMethodRepo.Update(pm);
        await _paymentMethodRepo.SaveChangesAsync(ct);

        return pm;
    }
}
