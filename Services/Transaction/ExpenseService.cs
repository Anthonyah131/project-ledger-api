using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Expense service. CRUD with soft delete.
/// Validates the monthly expenses limit according to the project owner's plan.
/// </summary>
public class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenseRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IObligationRepository _obligationRepo;
    private readonly IProjectPaymentMethodRepository _ppmRepo;
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly IExpenseSplitRepository _expenseSplitRepo;
    private readonly IProjectPartnerRepository _projectPartnerRepo;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IAuditLogService _auditLog;
    private readonly ITransactionCurrencyExchangeService _exchangeService;

    public ExpenseService(
        IExpenseRepository expenseRepo,
        IProjectRepository projectRepo,
        IObligationRepository obligationRepo,
        IProjectPaymentMethodRepository ppmRepo,
        IPaymentMethodRepository paymentMethodRepo,
        IExpenseSplitRepository expenseSplitRepo,
        IProjectPartnerRepository projectPartnerRepo,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLog,
        ITransactionCurrencyExchangeService exchangeService)
    {
        _expenseRepo = expenseRepo;
        _projectRepo = projectRepo;
        _obligationRepo = obligationRepo;
        _ppmRepo = ppmRepo;
        _paymentMethodRepo = paymentMethodRepo;
        _expenseSplitRepo = expenseSplitRepo;
        _projectPartnerRepo = projectPartnerRepo;
        _planAuth = planAuth;
        _auditLog = auditLog;
        _exchangeService = exchangeService;
    }

    public async Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var expense = await _expenseRepo.GetByIdAsync(id, ct);
        return expense is { ExpIsDeleted: false } ? expense : null;
    }

    public async Task<IEnumerable<Expense>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await _expenseRepo.GetByProjectIdAsync(projectId, ct);

    public async Task<IEnumerable<Expense>> GetByProjectIdAsync(Guid projectId, bool includeDeleted, CancellationToken ct = default)
        => await _expenseRepo.GetByProjectIdAsync(projectId, includeDeleted, ct);

    public async Task<(IReadOnlyList<Expense> Items, int TotalCount)> GetByProjectIdPagedAsync(
        Guid projectId, bool includeDeleted, bool? isActive, int skip, int take, string? sortBy, bool descending, DateOnly? from, DateOnly? to, CancellationToken ct = default)
        => await _expenseRepo.GetByProjectIdPagedAsync(projectId, includeDeleted, isActive, skip, take, sortBy, descending, from, to, ct);

    public async Task<IEnumerable<Expense>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default)
        => await _expenseRepo.GetByCategoryIdAsync(categoryId, ct);

    public async Task<IEnumerable<Expense>> GetByObligationIdAsync(Guid obligationId, CancellationToken ct = default)
        => await _expenseRepo.GetByObligationIdAsync(obligationId, ct);

    public async Task<IEnumerable<Expense>> GetTemplatesByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await _expenseRepo.GetTemplatesByProjectIdAsync(projectId, ct);

    public async Task<Expense> CreateAsync(Expense expense, IReadOnlyList<SplitInput>? splits = null, CancellationToken ct = default)
    {
        if (expense.ExpIsActive)
            ValidateAccountingReadinessForActivation(expense);

        var project = await _projectRepo.GetByIdAsync(expense.ExpProjectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        // Only validate expense limits for regular expenses (not templates)
        if (!expense.ExpIsTemplate)
        {
            // Count expenses for the current month (no templates, no deleted)
            var projectExpenses = await _expenseRepo.GetByProjectIdAsync(expense.ExpProjectId, ct);
            var thisMonthCount = projectExpenses
                .Count(e => !e.ExpIsTemplate
                    && e.ExpCreatedAt.Year == DateTime.UtcNow.Year
                    && e.ExpCreatedAt.Month == DateTime.UtcNow.Month);

            await _planAuth.ValidateLimitAsync(
                project.PrjOwnerUserId, PlanLimits.MaxExpensesPerMonth, thisMonthCount, ct);
        }

        // Validate that the payment method is linked to the project
        var isLinked = await _ppmRepo.IsPaymentMethodLinkedToProjectAsync(
            expense.ExpProjectId, expense.ExpPaymentMethodId, ct);

        if (!isLinked)
            throw new InvalidOperationException("PaymentMethodNotLinkedToProject");

        // Resolve amount and currency in the payment method's currency
        var paymentMethod = await _paymentMethodRepo.GetByIdAsync(expense.ExpPaymentMethodId, ct)
            ?? throw new KeyNotFoundException("PaymentMethodNotFound");

        expense.ExpAccountCurrency = paymentMethod.PmtCurrency;
        expense.ExpAccountAmount = ResolveAccountAmount(
            expense, paymentMethod.PmtCurrency, project.PrjCurrencyCode);

        // Validate that the obligation belongs to the same project and is not overpaid,
        // only when the expense is active.
        if (expense.ExpObligationId.HasValue && expense.ExpIsActive)
        {
            var obligation = await _obligationRepo.GetByIdAsync(expense.ExpObligationId.Value, ct)
                ?? throw new KeyNotFoundException("ObligationNotFound");

            if (obligation.OblIsDeleted)
                throw new KeyNotFoundException("ObligationNotFound");

            if (obligation.OblProjectId != expense.ExpProjectId)
                throw new InvalidOperationException("ObligationProjectMismatch");

            // Convert amounts to the obligation's currency to compare them correctly
            var existingPayments = await _expenseRepo.GetByObligationIdAsync(obligation.OblId, ct);
            var currentPaid = existingPayments.Sum(e =>
                AmountInObligationCurrency(
                    e.ExpOriginalAmount,
                    e.ExpOriginalCurrency,
                    e.ExpConvertedAmount,
                    e.ExpObligationEquivalentAmount,
                    obligation.OblCurrency,
                    requireEquivalentForCrossCurrency: false));

            var newPaymentAmount = AmountInObligationCurrency(
                expense.ExpOriginalAmount,
                expense.ExpOriginalCurrency,
                expense.ExpConvertedAmount,
                expense.ExpObligationEquivalentAmount,
                obligation.OblCurrency,
                requireEquivalentForCrossCurrency: true);

            if (currentPaid >= obligation.OblTotalAmount)
                throw new InvalidOperationException("ObligationAlreadyPaid");

            if (currentPaid + newPaymentAmount > obligation.OblTotalAmount)
                throw new InvalidOperationException("PaymentExceedsObligationTotal");
        }

        expense.ExpCreatedAt = DateTime.UtcNow;
        expense.ExpUpdatedAt = DateTime.UtcNow;

        await _expenseRepo.ExecuteInTransactionAsync(async (ct) =>
        {
            await _expenseRepo.AddAsync(expense, ct);
            await _expenseRepo.SaveChangesAsync(ct);

            // Create splits: explicit (if partners_enabled and provided) or auto-split
            if (splits is { Count: > 0 } && project.PrjPartnersEnabled)
            {
                var splitEntities = await BuildExpenseSplitsAsync(expense.ExpId, expense.ExpOriginalAmount, expense.ExpProjectId, splits, ct);
                foreach (var s in splitEntities)
                    await _expenseSplitRepo.AddAsync(s, ct);
                await _expenseSplitRepo.SaveChangesAsync(ct);
            }
            else if (paymentMethod.PmtOwnerPartnerId.HasValue)
            {
                var autoSplit = new ExpenseSplit
                {
                    ExsId = Guid.NewGuid(),
                    ExsExpenseId = expense.ExpId,
                    ExsPartnerId = paymentMethod.PmtOwnerPartnerId.Value,
                    ExsSplitType = "percentage",
                    ExsSplitValue = 100m,
                    ExsResolvedAmount = expense.ExpOriginalAmount
                };
                await _expenseSplitRepo.AddAsync(autoSplit, ct);
                await _expenseSplitRepo.SaveChangesAsync(ct);
            }

            // Audit expense creation
            await _auditLog.LogAsync("Expense", expense.ExpId, "create", expense.ExpCreatedByUserId,
                newValues: new { expense.ExpId, expense.ExpTitle, expense.ExpConvertedAmount, expense.ExpProjectId }, ct: ct);

            // Audit obligation association if applicable
            if (expense.ExpObligationId.HasValue)
            {
                await _auditLog.LogAsync("Obligation", expense.ExpObligationId.Value, "associate",
                    expense.ExpCreatedByUserId,
                    newValues: new { ExpenseId = expense.ExpId, Amount = expense.ExpConvertedAmount }, ct: ct);
            }
        }, ct);

        return expense;
    }

    public async Task UpdateAsync(Expense expense, IReadOnlyList<SplitInput>? splits = null, CancellationToken ct = default)
    {
        if (expense.ExpIsActive)
            ValidateAccountingReadinessForActivation(expense);

        var project = await _projectRepo.GetByIdAsync(expense.ExpProjectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        var paymentMethod = await _paymentMethodRepo.GetByIdAsync(expense.ExpPaymentMethodId, ct)
            ?? throw new KeyNotFoundException("PaymentMethodNotFound");

        expense.ExpAccountCurrency = paymentMethod.PmtCurrency;
        expense.ExpAccountAmount = ResolveAccountAmount(
            expense, paymentMethod.PmtCurrency, project.PrjCurrencyCode);

        // Validate overpayment only for active payments linked to an obligation.
        if (expense.ExpObligationId.HasValue && expense.ExpIsActive)
        {
            var obligation = await _obligationRepo.GetByIdAsync(expense.ExpObligationId.Value, ct);
            if (obligation is not null && !obligation.OblIsDeleted)
            {
                var existingPayments = await _expenseRepo.GetByObligationIdAsync(obligation.OblId, ct);
                var othersPaid = existingPayments
                    .Where(e => e.ExpId != expense.ExpId)
                    .Sum(e => AmountInObligationCurrency(
                        e.ExpOriginalAmount,
                        e.ExpOriginalCurrency,
                        e.ExpConvertedAmount,
                        e.ExpObligationEquivalentAmount,
                        obligation.OblCurrency,
                        requireEquivalentForCrossCurrency: false));

                var updatedAmount = AmountInObligationCurrency(
                    expense.ExpOriginalAmount,
                    expense.ExpOriginalCurrency,
                    expense.ExpConvertedAmount,
                    expense.ExpObligationEquivalentAmount,
                    obligation.OblCurrency,
                    requireEquivalentForCrossCurrency: true);

                if (othersPaid + updatedAmount > obligation.OblTotalAmount)
                    throw new InvalidOperationException("PaymentExceedsObligationTotal");
            }
        }

        expense.ExpUpdatedAt = DateTime.UtcNow;

        await _expenseRepo.ExecuteInTransactionAsync(async (ct) =>
        {
            _expenseRepo.Update(expense);
            await _expenseRepo.SaveChangesAsync(ct);

            // Update splits only if explicitly provided
            // null → do not modify; empty list → delete all; list with items → replace
            if (splits is not null)
            {
                await _expenseSplitRepo.DeleteByExpenseIdAsync(expense.ExpId, ct);
                if (splits.Count > 0 && project.PrjPartnersEnabled)
                {
                    var splitEntities = await BuildExpenseSplitsAsync(expense.ExpId, expense.ExpOriginalAmount, expense.ExpProjectId, splits, ct);
                    foreach (var s in splitEntities)
                        await _expenseSplitRepo.AddAsync(s, ct);
                }
                await _expenseSplitRepo.SaveChangesAsync(ct);
            }
        }, ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var expense = await _expenseRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("ExpenseNotFound");

        if (expense.ExpIsDeleted)
            throw new KeyNotFoundException("ExpenseNotFound");

        expense.ExpIsDeleted = true;
        expense.ExpDeletedAt = DateTime.UtcNow;
        expense.ExpDeletedByUserId = deletedByUserId;
        expense.ExpUpdatedAt = DateTime.UtcNow;

        _expenseRepo.Update(expense);
        await _expenseRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("Expense", id, "delete", deletedByUserId,
            oldValues: new { expense.ExpTitle, expense.ExpConvertedAmount }, ct: ct);
    }

    public async Task<IEnumerable<Expense>> GetByPaymentMethodIdAsync(
        Guid paymentMethodId, CancellationToken ct = default)
        => await _expenseRepo.GetByPaymentMethodIdAsync(paymentMethodId, ct);

    public async Task<(IReadOnlyList<Expense> Items, int TotalCount, decimal TotalActiveAmount)> GetByPaymentMethodIdPagedAsync(
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
        => await _expenseRepo.GetByPaymentMethodIdPagedAsync(
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

    /// <summary>
    /// Converts a payment amount to the obligation's currency.
    /// - If originalCurrency matches obligation: uses originalAmount.
    /// - If differs and obligationEquivalentAmount exists: uses obligationEquivalentAmount.
    /// - If differs and no equivalent:
    ///   - requireEquivalentForCrossCurrency=true: throws 400.
    ///   - requireEquivalentForCrossCurrency=false: legacy fallback to convertedAmount.
    /// </summary>
    private static decimal AmountInObligationCurrency(
        decimal originalAmount,
        string originalCurrency,
        decimal convertedAmount,
        decimal? obligationEquivalentAmount,
        string obligationCurrency,
        bool requireEquivalentForCrossCurrency)
    {
        if (string.Equals(originalCurrency, obligationCurrency, StringComparison.OrdinalIgnoreCase))
            return originalAmount;

        if (obligationEquivalentAmount is > 0)
            return obligationEquivalentAmount.Value;

        if (requireEquivalentForCrossCurrency)
            throw new InvalidOperationException("EquivalentAmountRequiredForCrossCurrency");

        // Compatibility with historical payments without persisted equivalent.
        return convertedAmount;
    }

    private static decimal ResolveAccountAmount(
        Expense expense,
        string paymentMethodCurrency,
        string projectCurrency)
    {
        if (expense.ExpAccountAmount is > 0)
            return expense.ExpAccountAmount.Value;

        if (string.Equals(paymentMethodCurrency, expense.ExpOriginalCurrency, StringComparison.OrdinalIgnoreCase))
            return expense.ExpOriginalAmount;

        if (string.Equals(paymentMethodCurrency, projectCurrency, StringComparison.OrdinalIgnoreCase))
            return expense.ExpConvertedAmount;

        throw new InvalidOperationException("AccountAmountRequiredForDistinctCurrencies");
    }

    private static void ValidateAccountingReadinessForActivation(Expense expense)
    {
        if (expense.ExpOriginalAmount <= 0)
            throw new InvalidOperationException("AmountMustBePositive");

        if (expense.ExpConvertedAmount <= 0)
            throw new InvalidOperationException("AmountMustBePositive");

        if (expense.ExpExchangeRate <= 0)
            throw new InvalidOperationException("ExchangeRateMustBePositive");

        if (string.IsNullOrWhiteSpace(expense.ExpOriginalCurrency) || expense.ExpOriginalCurrency.Length != 3)
            throw new InvalidOperationException("InvalidCurrencyCode");

        if (string.IsNullOrWhiteSpace(expense.ExpTitle))
            throw new InvalidOperationException("TitleRequired");

        if (expense.ExpExpenseDate == default)
            throw new InvalidOperationException("DateRequired");
    }

    public async Task<IReadOnlyList<Expense>> BulkCreateAsync(
        IReadOnlyList<(Expense Expense, IReadOnlyList<SplitInput>? Splits, IReadOnlyList<TransactionExchangeInput>? Exchanges)> items,
        CancellationToken ct = default)
    {
        if (items.Count == 0)
            return [];

        var projectId = items[0].Expense.ExpProjectId;

        var project = await _projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        // Validate plan limit for the entire batch at once (only non-template expenses)
        var projectExpenses = await _expenseRepo.GetByProjectIdAsync(projectId, ct);
        var thisMonthCount = projectExpenses
            .Count(e => !e.ExpIsTemplate
                && e.ExpCreatedAt.Year == DateTime.UtcNow.Year
                && e.ExpCreatedAt.Month == DateTime.UtcNow.Month);

        await _planAuth.ValidateLimitAsync(
            project.PrjOwnerUserId, PlanLimits.MaxExpensesPerMonth, thisMonthCount + items.Count - 1, ct);

        // Payment method cache to avoid duplicate queries within the batch
        var paymentMethodCache = new Dictionary<Guid, PaymentMethod>();

        var now = DateTime.UtcNow;
        foreach (var (expense, _, _) in items)
        {
            ValidateAccountingReadinessForActivation(expense);

            if (!paymentMethodCache.TryGetValue(expense.ExpPaymentMethodId, out var paymentMethod))
            {
                var isLinked = await _ppmRepo.IsPaymentMethodLinkedToProjectAsync(
                    projectId, expense.ExpPaymentMethodId, ct);
                if (!isLinked)
                    throw new InvalidOperationException("PaymentMethodNotLinkedToProject");

                paymentMethod = await _paymentMethodRepo.GetByIdAsync(expense.ExpPaymentMethodId, ct)
                    ?? throw new KeyNotFoundException("PaymentMethodNotFound");
                paymentMethodCache[expense.ExpPaymentMethodId] = paymentMethod;
            }

            expense.ExpAccountCurrency = paymentMethod.PmtCurrency;
            expense.ExpAccountAmount = ResolveAccountAmount(expense, paymentMethod.PmtCurrency, project.PrjCurrencyCode);
            expense.ExpCreatedAt = now;
            expense.ExpUpdatedAt = now;
        }

        await _expenseRepo.ExecuteInTransactionAsync(async (ct) =>
        {
            foreach (var (expense, splits, exchanges) in items)
            {
                // Validate obligation if applicable — must be done within the transaction
                // so overpayment validations include items already saved from the same batch.
                if (expense.ExpObligationId.HasValue && expense.ExpIsActive)
                {
                    var obligation = await _obligationRepo.GetByIdAsync(expense.ExpObligationId.Value, ct)
                        ?? throw new KeyNotFoundException("ObligationNotFound");

                    if (obligation.OblIsDeleted)
                        throw new KeyNotFoundException("ObligationNotFound");

                    if (obligation.OblProjectId != expense.ExpProjectId)
                        throw new InvalidOperationException("ObligationProjectMismatch");

                    var existingPayments = await _expenseRepo.GetByObligationIdAsync(obligation.OblId, ct);
                    var currentPaid = existingPayments.Sum(e =>
                        AmountInObligationCurrency(
                            e.ExpOriginalAmount,
                            e.ExpOriginalCurrency,
                            e.ExpConvertedAmount,
                            e.ExpObligationEquivalentAmount,
                            obligation.OblCurrency,
                            requireEquivalentForCrossCurrency: false));

                    var newPaymentAmount = AmountInObligationCurrency(
                        expense.ExpOriginalAmount,
                        expense.ExpOriginalCurrency,
                        expense.ExpConvertedAmount,
                        expense.ExpObligationEquivalentAmount,
                        obligation.OblCurrency,
                        requireEquivalentForCrossCurrency: true);

                    if (currentPaid >= obligation.OblTotalAmount)
                        throw new InvalidOperationException("ObligationAlreadyPaid");

                    if (currentPaid + newPaymentAmount > obligation.OblTotalAmount)
                        throw new InvalidOperationException("PaymentExceedsObligationTotal");
                }

                await _expenseRepo.AddAsync(expense, ct);
                await _expenseRepo.SaveChangesAsync(ct);

                var paymentMethod = paymentMethodCache[expense.ExpPaymentMethodId];

                if (splits is { Count: > 0 } && project.PrjPartnersEnabled)
                {
                    var splitEntities = await BuildExpenseSplitsAsync(
                        expense.ExpId, expense.ExpOriginalAmount, projectId, splits, ct);
                    foreach (var s in splitEntities)
                        await _expenseSplitRepo.AddAsync(s, ct);
                    await _expenseSplitRepo.SaveChangesAsync(ct);
                }
                else if (paymentMethod.PmtOwnerPartnerId.HasValue)
                {
                    var autoSplit = new ExpenseSplit
                    {
                        ExsId = Guid.NewGuid(),
                        ExsExpenseId = expense.ExpId,
                        ExsPartnerId = paymentMethod.PmtOwnerPartnerId.Value,
                        ExsSplitType = "percentage",
                        ExsSplitValue = 100m,
                        ExsResolvedAmount = expense.ExpOriginalAmount
                    };
                    await _expenseSplitRepo.AddAsync(autoSplit, ct);
                    await _expenseSplitRepo.SaveChangesAsync(ct);
                }

                if (exchanges is { Count: > 0 })
                    await _exchangeService.SaveExchangesAsync("expense", expense.ExpId, exchanges, ct);

                await _auditLog.LogAsync("Expense", expense.ExpId, "create", expense.ExpCreatedByUserId,
                    newValues: new { expense.ExpId, expense.ExpTitle, expense.ExpConvertedAmount, expense.ExpProjectId }, ct: ct);

                if (expense.ExpObligationId.HasValue)
                {
                    await _auditLog.LogAsync("Obligation", expense.ExpObligationId.Value, "associate",
                        expense.ExpCreatedByUserId,
                        newValues: new { ExpenseId = expense.ExpId, Amount = expense.ExpConvertedAmount }, ct: ct);
                }
            }
        }, ct);

        return items.Select(i => i.Expense).ToList();
    }

    private async Task<IReadOnlyList<ExpenseSplit>> BuildExpenseSplitsAsync(
        Guid expenseId,
        decimal originalAmount,
        Guid projectId,
        IReadOnlyList<SplitInput> splits,
        CancellationToken ct)
    {
        // No duplicate partners
        var partnerIds = splits.Select(s => s.PartnerId).ToList();
        if (partnerIds.Distinct().Count() != partnerIds.Count)
            throw new InvalidOperationException("DuplicatePartnerInSplits");

        // All partners must be active in project
        var projectPartners = await _projectPartnerRepo.GetByProjectIdAsync(projectId, ct);
        var validPartnerIds = projectPartners.Select(pp => pp.PtpPartnerId).ToHashSet();
        var invalid = partnerIds.FirstOrDefault(id => !validPartnerIds.Contains(id));
        if (invalid != default)
            throw new InvalidOperationException("PartnerNotAssignedToProject");

        // No mixed types
        var types = splits.Select(s => s.SplitType).Distinct().ToList();
        if (types.Count > 1)
            throw new InvalidOperationException("CannotMixSplitTypes");

        var splitType = types[0];

        if (splitType == "percentage")
        {
            var sum = splits.Sum(s => s.SplitValue);
            if (Math.Abs(sum - 100m) > 0.01m)
                throw new InvalidOperationException("PercentageSplitsMustSum100");
        }
        else if (splitType == "fixed")
        {
            var sum = splits.Sum(s => s.SplitValue);
            if (Math.Abs(sum - originalAmount) > 0.01m)
                throw new InvalidOperationException("FixedSplitsMustSumTotal");
        }

        return splits.Select(s =>
        {
            var splitId = Guid.NewGuid();
            return new ExpenseSplit
            {
                ExsId = splitId,
                ExsExpenseId = expenseId,
                ExsPartnerId = s.PartnerId,
                ExsSplitType = s.SplitType,
                ExsSplitValue = s.SplitValue,
                ExsResolvedAmount = s.ResolvedAmount,
                CurrencyExchanges = s.CurrencyExchanges?.Select(ce => new SplitCurrencyExchange
                {
                    SceId = Guid.NewGuid(),
                    SceExpenseSplitId = splitId,
                    SceCurrencyCode = ce.CurrencyCode,
                    SceExchangeRate = ce.ExchangeRate,
                    SceConvertedAmount = ce.ConvertedAmount,
                    SceCreatedAt = DateTime.UtcNow
                }).ToList() ?? []
            };
        }).ToList();
    }
}
