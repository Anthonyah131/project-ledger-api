using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de gastos. CRUD con soft delete.
/// Valida límite de gastos por mes según el plan del owner del proyecto.
/// </summary>
public class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenseRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IObligationRepository _obligationRepo;
    private readonly IProjectPaymentMethodRepository _ppmRepo;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IAuditLogService _auditLog;

    public ExpenseService(
        IExpenseRepository expenseRepo,
        IProjectRepository projectRepo,
        IObligationRepository obligationRepo,
        IProjectPaymentMethodRepository ppmRepo,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLog)
    {
        _expenseRepo = expenseRepo;
        _projectRepo = projectRepo;
        _obligationRepo = obligationRepo;
        _ppmRepo = ppmRepo;
        _planAuth = planAuth;
        _auditLog = auditLog;
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
        Guid projectId, bool includeDeleted, bool? isActive, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default)
        => await _expenseRepo.GetByProjectIdPagedAsync(projectId, includeDeleted, isActive, skip, take, sortBy, descending, ct);

    public async Task<IEnumerable<Expense>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default)
        => await _expenseRepo.GetByCategoryIdAsync(categoryId, ct);

    public async Task<IEnumerable<Expense>> GetByObligationIdAsync(Guid obligationId, CancellationToken ct = default)
        => await _expenseRepo.GetByObligationIdAsync(obligationId, ct);

    public async Task<IEnumerable<Expense>> GetTemplatesByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await _expenseRepo.GetTemplatesByProjectIdAsync(projectId, ct);

    public async Task<Expense> CreateAsync(Expense expense, CancellationToken ct = default)
    {
        if (expense.ExpIsActive)
            ValidateAccountingReadinessForActivation(expense);

        var project = await _projectRepo.GetByIdAsync(expense.ExpProjectId, ct)
            ?? throw new KeyNotFoundException($"Project '{expense.ExpProjectId}' not found.");

        // Solo validar límite de gastos para gastos normales (no templates)
        if (!expense.ExpIsTemplate)
        {
            // Contar gastos del mes actual (no templates, no eliminados)
            var projectExpenses = await _expenseRepo.GetByProjectIdAsync(expense.ExpProjectId, ct);
            var thisMonthCount = projectExpenses
                .Count(e => !e.ExpIsTemplate
                    && e.ExpCreatedAt.Year == DateTime.UtcNow.Year
                    && e.ExpCreatedAt.Month == DateTime.UtcNow.Month);

            await _planAuth.ValidateLimitAsync(
                project.PrjOwnerUserId, PlanLimits.MaxExpensesPerMonth, thisMonthCount, ct);
        }

        // Validar que el método de pago está vinculado al proyecto
        var isLinked = await _ppmRepo.IsPaymentMethodLinkedToProjectAsync(
            expense.ExpProjectId, expense.ExpPaymentMethodId, ct);

        if (!isLinked)
            throw new InvalidOperationException(
                "The payment method is not linked to this project. " +
                "Link it first via /api/projects/{projectId}/payment-methods.");

        // Validar que la obligación pertenece al mismo proyecto y no se sobre-paga
        // solo cuando el gasto está activo.
        if (expense.ExpObligationId.HasValue && expense.ExpIsActive)
        {
            var obligation = await _obligationRepo.GetByIdAsync(expense.ExpObligationId.Value, ct)
                ?? throw new KeyNotFoundException(
                    $"Obligation '{expense.ExpObligationId}' not found.");

            if (obligation.OblIsDeleted)
                throw new KeyNotFoundException(
                    $"Obligation '{expense.ExpObligationId}' not found.");

            if (obligation.OblProjectId != expense.ExpProjectId)
                throw new InvalidOperationException(
                    "La obligación no pertenece al mismo proyecto que el gasto.");

            // Convertir montos a la moneda de la obligación para comparar correctamente
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
                throw new InvalidOperationException(
                    "This obligation is already fully paid. No additional payments are allowed.");

            if (currentPaid + newPaymentAmount > obligation.OblTotalAmount)
                throw new InvalidOperationException(
                    $"Payment would exceed the obligation total. " +
                    $"Remaining: {obligation.OblTotalAmount - currentPaid:F2} {obligation.OblCurrency}, " +
                    $"Attempted: {newPaymentAmount:F2} {obligation.OblCurrency}.");
        }

        expense.ExpCreatedAt = DateTime.UtcNow;
        expense.ExpUpdatedAt = DateTime.UtcNow;

        await _expenseRepo.AddAsync(expense, ct);
        await _expenseRepo.SaveChangesAsync(ct);

        // Auditar creación del gasto
        await _auditLog.LogAsync("Expense", expense.ExpId, "create", expense.ExpCreatedByUserId,
            newValues: new { expense.ExpId, expense.ExpTitle, expense.ExpConvertedAmount, expense.ExpProjectId }, ct: ct);

        // Auditar asociación a obligación si aplica
        if (expense.ExpObligationId.HasValue)
        {
            await _auditLog.LogAsync("Obligation", expense.ExpObligationId.Value, "associate",
                expense.ExpCreatedByUserId,
                newValues: new { ExpenseId = expense.ExpId, Amount = expense.ExpConvertedAmount }, ct: ct);
        }

        return expense;
    }

    public async Task UpdateAsync(Expense expense, CancellationToken ct = default)
    {
        if (expense.ExpIsActive)
            ValidateAccountingReadinessForActivation(expense);

        // Validar sobre-pago solo para pagos activos vinculados a obligación.
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
                    throw new InvalidOperationException(
                        $"Payment would exceed the obligation total. " +
                        $"Remaining (excluding this expense): {obligation.OblTotalAmount - othersPaid:F2} {obligation.OblCurrency}, " +
                        $"Attempted: {updatedAmount:F2} {obligation.OblCurrency}.");
            }
        }

        expense.ExpUpdatedAt = DateTime.UtcNow;
        _expenseRepo.Update(expense);
        await _expenseRepo.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var expense = await _expenseRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Expense '{id}' not found.");

        if (expense.ExpIsDeleted)
            throw new KeyNotFoundException($"Expense '{id}' not found.");

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

    public async Task<(IReadOnlyList<Expense> Items, int TotalCount)> GetByPaymentMethodIdPagedAsync(
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
    /// Convierte el monto de un pago a la moneda de la obligación.
    /// - Si originalCurrency coincide con obligación: usa originalAmount.
    /// - Si difiere y existe obligationEquivalentAmount: usa obligationEquivalentAmount.
    /// - Si difiere y no hay equivalente:
    ///   - requireEquivalentForCrossCurrency=true: lanza 400.
    ///   - requireEquivalentForCrossCurrency=false: fallback legado a convertedAmount.
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
            throw new InvalidOperationException(
                $"Se requiere el equivalente en {obligationCurrency} para este pago.");

        // Compatibilidad con pagos históricos sin equivalente persistido.
        return convertedAmount;
    }

    private static void ValidateAccountingReadinessForActivation(Expense expense)
    {
        if (expense.ExpOriginalAmount <= 0)
            throw new InvalidOperationException("Cannot activate expense: OriginalAmount must be greater than 0.");

        if (expense.ExpConvertedAmount <= 0)
            throw new InvalidOperationException("Cannot activate expense: ConvertedAmount must be greater than 0.");

        if (expense.ExpExchangeRate <= 0)
            throw new InvalidOperationException("Cannot activate expense: ExchangeRate must be greater than 0.");

        if (string.IsNullOrWhiteSpace(expense.ExpOriginalCurrency) || expense.ExpOriginalCurrency.Length != 3)
            throw new InvalidOperationException("Cannot activate expense: OriginalCurrency must be a valid 3-letter ISO code.");

        if (string.IsNullOrWhiteSpace(expense.ExpTitle))
            throw new InvalidOperationException("Cannot activate expense: Title is required.");

        if (expense.ExpExpenseDate == default)
            throw new InvalidOperationException("Cannot activate expense: ExpenseDate is required.");
    }
}
