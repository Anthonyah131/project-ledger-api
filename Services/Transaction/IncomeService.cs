using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de ingresos. CRUD con soft delete.
/// Valida límite de ingresos por mes según el plan del owner del proyecto.
/// </summary>
public class IncomeService : IIncomeService
{
    private readonly IIncomeRepository _incomeRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IProjectPaymentMethodRepository _ppmRepo;
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly IIncomeSplitRepository _incomeSplitRepo;
    private readonly IProjectPartnerRepository _projectPartnerRepo;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IAuditLogService _auditLog;

    public IncomeService(
        IIncomeRepository incomeRepo,
        IProjectRepository projectRepo,
        IProjectPaymentMethodRepository ppmRepo,
        IPaymentMethodRepository paymentMethodRepo,
        IIncomeSplitRepository incomeSplitRepo,
        IProjectPartnerRepository projectPartnerRepo,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLog)
    {
        _incomeRepo = incomeRepo;
        _projectRepo = projectRepo;
        _ppmRepo = ppmRepo;
        _paymentMethodRepo = paymentMethodRepo;
        _incomeSplitRepo = incomeSplitRepo;
        _projectPartnerRepo = projectPartnerRepo;
        _planAuth = planAuth;
        _auditLog = auditLog;
    }

    public async Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var income = await _incomeRepo.GetByIdAsync(id, ct);
        return income is { IncIsDeleted: false } ? income : null;
    }

    public async Task<IEnumerable<Income>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await _incomeRepo.GetByProjectIdAsync(projectId, ct);

    public async Task<IEnumerable<Income>> GetByProjectIdAsync(Guid projectId, bool includeDeleted, CancellationToken ct = default)
        => await _incomeRepo.GetByProjectIdAsync(projectId, includeDeleted, ct);

    public async Task<(IReadOnlyList<Income> Items, int TotalCount)> GetByProjectIdPagedAsync(
        Guid projectId, bool includeDeleted, bool? isActive, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default)
        => await _incomeRepo.GetByProjectIdPagedAsync(projectId, includeDeleted, isActive, skip, take, sortBy, descending, ct);

    public async Task<IEnumerable<Income>> GetByPaymentMethodIdAsync(
        Guid paymentMethodId, CancellationToken ct = default)
        => await _incomeRepo.GetByPaymentMethodIdAsync(paymentMethodId, ct);

    public async Task<(IReadOnlyList<Income> Items, int TotalCount, decimal TotalActiveAmount)> GetByPaymentMethodIdPagedAsync(
        Guid paymentMethodId,
        bool? isActive,
        int skip,
        int take,
        string? sortBy,
        bool descending,
        DateOnly? from,
        DateOnly? to,
        Guid? projectId,
        CancellationToken ct = default)
        => await _incomeRepo.GetByPaymentMethodIdPagedAsync(
            paymentMethodId,
            isActive,
            skip,
            take,
            sortBy,
            descending,
            from,
            to,
            projectId,
            ct);

    public async Task<Income> CreateAsync(Income income, IReadOnlyList<SplitInput>? splits = null, CancellationToken ct = default)
    {
        if (income.IncIsActive)
            ValidateAccountingReadinessForActivation(income);

        var project = await _projectRepo.GetByIdAsync(income.IncProjectId, ct)
            ?? throw new KeyNotFoundException($"Project '{income.IncProjectId}' not found.");

        // Validar límite de ingresos por mes
        var projectIncomes = await _incomeRepo.GetByProjectIdAsync(income.IncProjectId, ct);
        var thisMonthCount = projectIncomes
            .Count(i => i.IncCreatedAt.Year == DateTime.UtcNow.Year
                     && i.IncCreatedAt.Month == DateTime.UtcNow.Month);

        await _planAuth.ValidateLimitAsync(
            project.PrjOwnerUserId, PlanLimits.MaxIncomesPerMonth, thisMonthCount, ct);

        // Validar que la cuenta destino está vinculada al proyecto
        var paymentMethod = await GetLinkedPaymentMethodAsync(
            income.IncProjectId, income.IncPaymentMethodId, ct);

        income.IncAccountCurrency = paymentMethod.PmtCurrency;
        income.IncAccountAmount = ResolveAccountAmount(
            income,
            paymentMethod.PmtCurrency,
            project.PrjCurrencyCode);

        income.IncCreatedAt = DateTime.UtcNow;
        income.IncUpdatedAt = DateTime.UtcNow;

        await _incomeRepo.ExecuteInTransactionAsync(async (ct) =>
        {
            await _incomeRepo.AddAsync(income, ct);
            await _incomeRepo.SaveChangesAsync(ct);

            // Crear splits: explícitos (si partners_enabled y se proveyeron) o auto-split
            if (splits is { Count: > 0 } && project.PrjPartnersEnabled)
            {
                var splitEntities = await BuildIncomeSplitsAsync(income.IncId, income.IncOriginalAmount, income.IncProjectId, splits, ct);
                foreach (var s in splitEntities)
                    await _incomeSplitRepo.AddAsync(s, ct);
                await _incomeSplitRepo.SaveChangesAsync(ct);
            }
            else if (paymentMethod.PmtOwnerPartnerId.HasValue)
            {
                var autoSplit = new IncomeSplit
                {
                    InsId = Guid.NewGuid(),
                    InsIncomeId = income.IncId,
                    InsPartnerId = paymentMethod.PmtOwnerPartnerId.Value,
                    InsSplitType = "percentage",
                    InsSplitValue = 100m,
                    InsResolvedAmount = income.IncOriginalAmount
                };
                await _incomeSplitRepo.AddAsync(autoSplit, ct);
                await _incomeSplitRepo.SaveChangesAsync(ct);
            }

            await _auditLog.LogAsync("Income", income.IncId, "create", income.IncCreatedByUserId,
                newValues: new
                {
                    income.IncId,
                    income.IncTitle,
                    income.IncConvertedAmount,
                    income.IncProjectId,
                    income.IncAccountAmount,
                    income.IncAccountCurrency
                },
                ct: ct);
        }, ct);

        return income;
    }

    public async Task UpdateAsync(Income income, IReadOnlyList<SplitInput>? splits = null, CancellationToken ct = default)
    {
        if (income.IncIsActive)
            ValidateAccountingReadinessForActivation(income);

        var project = await _projectRepo.GetByIdAsync(income.IncProjectId, ct)
            ?? throw new KeyNotFoundException($"Project '{income.IncProjectId}' not found.");

        var paymentMethod = await GetLinkedPaymentMethodAsync(
            income.IncProjectId, income.IncPaymentMethodId, ct);

        income.IncAccountCurrency = paymentMethod.PmtCurrency;
        income.IncAccountAmount = ResolveAccountAmount(
            income,
            paymentMethod.PmtCurrency,
            project.PrjCurrencyCode);

        income.IncUpdatedAt = DateTime.UtcNow;

        await _incomeRepo.ExecuteInTransactionAsync(async (ct) =>
        {
            _incomeRepo.Update(income);
            await _incomeRepo.SaveChangesAsync(ct);

            // Actualizar splits solo si se proveyeron explícitamente
            // null → no modificar; lista vacía → eliminar todos; lista con items → reemplazar
            if (splits is not null)
            {
                await _incomeSplitRepo.DeleteByIncomeIdAsync(income.IncId, ct);
                if (splits.Count > 0 && project.PrjPartnersEnabled)
                {
                    var splitEntities = await BuildIncomeSplitsAsync(income.IncId, income.IncOriginalAmount, income.IncProjectId, splits, ct);
                    foreach (var s in splitEntities)
                        await _incomeSplitRepo.AddAsync(s, ct);
                }
                await _incomeSplitRepo.SaveChangesAsync(ct);
            }
        }, ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var income = await _incomeRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Income '{id}' not found.");

        if (income.IncIsDeleted)
            throw new KeyNotFoundException($"Income '{id}' not found.");

        income.IncIsDeleted = true;
        income.IncDeletedAt = DateTime.UtcNow;
        income.IncDeletedByUserId = deletedByUserId;
        income.IncUpdatedAt = DateTime.UtcNow;

        _incomeRepo.Update(income);
        await _incomeRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("Income", id, "delete", deletedByUserId,
            oldValues: new { income.IncTitle, income.IncConvertedAmount }, ct: ct);
    }

    private async Task<PaymentMethod> GetLinkedPaymentMethodAsync(
        Guid projectId,
        Guid paymentMethodId,
        CancellationToken ct)
    {
        var isLinked = await _ppmRepo.IsPaymentMethodLinkedToProjectAsync(projectId, paymentMethodId, ct);
        if (!isLinked)
            throw new InvalidOperationException(
                "The payment method is not linked to this project. " +
                "Link it first via /api/projects/{projectId}/payment-methods.");

        var paymentMethod = await _paymentMethodRepo.GetByIdAsync(paymentMethodId, ct)
            ?? throw new KeyNotFoundException($"Payment method '{paymentMethodId}' not found.");

        return paymentMethod;
    }

    private static decimal ResolveAccountAmount(
        Income income,
        string paymentMethodCurrency,
        string projectCurrency)
    {
        if (income.IncAccountAmount is > 0)
            return income.IncAccountAmount.Value;

        if (string.Equals(paymentMethodCurrency, income.IncOriginalCurrency, StringComparison.OrdinalIgnoreCase))
            return income.IncOriginalAmount;

        if (string.Equals(paymentMethodCurrency, projectCurrency, StringComparison.OrdinalIgnoreCase))
            return income.IncConvertedAmount;

        throw new InvalidOperationException(
            "AccountAmount is required when payment method currency differs from both original and project currencies.");
    }

    private static void ValidateAccountingReadinessForActivation(Income income)
    {
        if (income.IncOriginalAmount <= 0)
            throw new InvalidOperationException("Cannot activate income: OriginalAmount must be greater than 0.");

        if (income.IncConvertedAmount <= 0)
            throw new InvalidOperationException("Cannot activate income: ConvertedAmount must be greater than 0.");

        if (income.IncExchangeRate <= 0)
            throw new InvalidOperationException("Cannot activate income: ExchangeRate must be greater than 0.");

        if (string.IsNullOrWhiteSpace(income.IncOriginalCurrency) || income.IncOriginalCurrency.Length != 3)
            throw new InvalidOperationException("Cannot activate income: OriginalCurrency must be a valid 3-letter ISO code.");

        if (string.IsNullOrWhiteSpace(income.IncTitle))
            throw new InvalidOperationException("Cannot activate income: Title is required.");

        if (income.IncIncomeDate == default)
            throw new InvalidOperationException("Cannot activate income: IncomeDate is required.");
    }

    private async Task<IReadOnlyList<IncomeSplit>> BuildIncomeSplitsAsync(
        Guid incomeId,
        decimal originalAmount,
        Guid projectId,
        IReadOnlyList<SplitInput> splits,
        CancellationToken ct)
    {
        // No duplicate partners
        var partnerIds = splits.Select(s => s.PartnerId).ToList();
        if (partnerIds.Distinct().Count() != partnerIds.Count)
            throw new InvalidOperationException("Duplicate partner found in splits.");

        // All partners must be active in project
        var projectPartners = await _projectPartnerRepo.GetByProjectIdAsync(projectId, ct);
        var validPartnerIds = projectPartners.Select(pp => pp.PtpPartnerId).ToHashSet();
        var invalid = partnerIds.FirstOrDefault(id => !validPartnerIds.Contains(id));
        if (invalid != default)
            throw new InvalidOperationException($"Partner '{invalid}' is not assigned to this project.");

        // No mixed types
        var types = splits.Select(s => s.SplitType).Distinct().ToList();
        if (types.Count > 1)
            throw new InvalidOperationException("Cannot mix 'percentage' and 'fixed' split types in the same income.");

        var splitType = types[0];

        if (splitType == "percentage")
        {
            var sum = splits.Sum(s => s.SplitValue);
            if (Math.Abs(sum - 100m) > 0.01m)
                throw new InvalidOperationException($"Percentage splits must sum to 100. Got: {sum}.");
        }
        else if (splitType == "fixed")
        {
            var sum = splits.Sum(s => s.SplitValue);
            if (Math.Abs(sum - originalAmount) > 0.01m)
                throw new InvalidOperationException($"Fixed splits must sum to {originalAmount}. Got: {sum}.");
        }

        return splits.Select(s =>
        {
            var splitId = Guid.NewGuid();
            return new IncomeSplit
            {
                InsId = splitId,
                InsIncomeId = incomeId,
                InsPartnerId = s.PartnerId,
                InsSplitType = s.SplitType,
                InsSplitValue = s.SplitValue,
                InsResolvedAmount = s.ResolvedAmount,
                CurrencyExchanges = s.CurrencyExchanges?.Select(ce => new SplitCurrencyExchange
                {
                    SceId = Guid.NewGuid(),
                    SceIncomeSplitId = splitId,
                    SceCurrencyCode = ce.CurrencyCode,
                    SceExchangeRate = ce.ExchangeRate,
                    SceConvertedAmount = ce.ConvertedAmount,
                    SceCreatedAt = DateTime.UtcNow
                }).ToList() ?? []
            };
        }).ToList();
    }
}
