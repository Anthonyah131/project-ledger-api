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
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IAuditLogService _auditLog;

    public ExpenseService(
        IExpenseRepository expenseRepo,
        IProjectRepository projectRepo,
        IObligationRepository obligationRepo,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLog)
    {
        _expenseRepo = expenseRepo;
        _projectRepo = projectRepo;
        _obligationRepo = obligationRepo;
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

    public async Task<IEnumerable<Expense>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default)
        => await _expenseRepo.GetByCategoryIdAsync(categoryId, ct);

    public async Task<IEnumerable<Expense>> GetByObligationIdAsync(Guid obligationId, CancellationToken ct = default)
        => await _expenseRepo.GetByObligationIdAsync(obligationId, ct);

    public async Task<IEnumerable<Expense>> GetTemplatesByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await _expenseRepo.GetTemplatesByProjectIdAsync(projectId, ct);

    public async Task<Expense> CreateAsync(Expense expense, CancellationToken ct = default)
    {
        // Solo validar límite de gastos para gastos normales (no templates)
        if (!expense.ExpIsTemplate)
        {
            var project = await _projectRepo.GetByIdAsync(expense.ExpProjectId, ct)
                ?? throw new KeyNotFoundException($"Project '{expense.ExpProjectId}' not found.");

            // Contar gastos del mes actual (no templates, no eliminados)
            var projectExpenses = await _expenseRepo.GetByProjectIdAsync(expense.ExpProjectId, ct);
            var thisMonthCount = projectExpenses
                .Count(e => !e.ExpIsTemplate
                    && e.ExpCreatedAt.Year == DateTime.UtcNow.Year
                    && e.ExpCreatedAt.Month == DateTime.UtcNow.Month);

            await _planAuth.ValidateLimitAsync(
                project.PrjOwnerUserId, PlanLimits.MaxExpensesPerMonth, thisMonthCount, ct);
        }

        // Validar que la obligación pertenece al mismo proyecto y no se sobre-paga
        if (expense.ExpObligationId.HasValue)
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

            // Bloquear sobre-pago: monto pagado + nuevo gasto no puede exceder el total
            var existingPayments = await _expenseRepo.GetByObligationIdAsync(obligation.OblId, ct);
            var currentPaid = existingPayments.Sum(e => e.ExpConvertedAmount);
            var newConverted = expense.ExpOriginalAmount * expense.ExpExchangeRate;

            if (currentPaid >= obligation.OblTotalAmount)
                throw new InvalidOperationException(
                    "This obligation is already fully paid. No additional payments are allowed.");

            if (currentPaid + newConverted > obligation.OblTotalAmount)
                throw new InvalidOperationException(
                    $"Payment would exceed the obligation total. " +
                    $"Remaining: {obligation.OblTotalAmount - currentPaid:F2}, " +
                    $"Attempted: {newConverted:F2}.");
        }

        // Calcular monto convertido
        expense.ExpConvertedAmount = expense.ExpOriginalAmount * expense.ExpExchangeRate;

        // Calcular monto alternativo si aplica
        if (expense.ExpAltCurrency is not null && expense.ExpAltExchangeRate.HasValue)
            expense.ExpAltAmount = expense.ExpOriginalAmount * expense.ExpAltExchangeRate.Value;

        expense.ExpCreatedAt = DateTime.UtcNow;
        expense.ExpUpdatedAt = DateTime.UtcNow;

        await _expenseRepo.AddAsync(expense, ct);
        await _expenseRepo.SaveChangesAsync(ct);

        // Auditar creación del gasto
        _ = _auditLog.LogAsync("Expense", expense.ExpId, "create", expense.ExpCreatedByUserId,
            newValues: new { expense.ExpId, expense.ExpTitle, expense.ExpConvertedAmount, expense.ExpProjectId }, ct: ct);

        // Auditar asociación a obligación si aplica
        if (expense.ExpObligationId.HasValue)
        {
            _ = _auditLog.LogAsync("Obligation", expense.ExpObligationId.Value, "associate",
                expense.ExpCreatedByUserId,
                newValues: new { ExpenseId = expense.ExpId, Amount = expense.ExpConvertedAmount }, ct: ct);
        }

        return expense;
    }

    public async Task UpdateAsync(Expense expense, CancellationToken ct = default)
    {
        // Recalcular montos
        expense.ExpConvertedAmount = expense.ExpOriginalAmount * expense.ExpExchangeRate;
        expense.ExpAltAmount = expense.ExpAltCurrency is not null && expense.ExpAltExchangeRate.HasValue
            ? expense.ExpOriginalAmount * expense.ExpAltExchangeRate.Value
            : null;

        // Validar sobre-pago si el gasto está vinculado a una obligación
        if (expense.ExpObligationId.HasValue)
        {
            var obligation = await _obligationRepo.GetByIdAsync(expense.ExpObligationId.Value, ct);
            if (obligation is not null && !obligation.OblIsDeleted)
            {
                var existingPayments = await _expenseRepo.GetByObligationIdAsync(obligation.OblId, ct);
                // Excluir el gasto actual del cálculo (se está actualizando)
                var othersPaid = existingPayments
                    .Where(e => e.ExpId != expense.ExpId)
                    .Sum(e => e.ExpConvertedAmount);

                if (othersPaid + expense.ExpConvertedAmount > obligation.OblTotalAmount)
                    throw new InvalidOperationException(
                        $"Payment would exceed the obligation total. " +
                        $"Remaining (excluding this expense): {obligation.OblTotalAmount - othersPaid:F2}, " +
                        $"Attempted: {expense.ExpConvertedAmount:F2}.");
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

        _ = _auditLog.LogAsync("Expense", id, "delete", deletedByUserId,
            oldValues: new { expense.ExpTitle, expense.ExpConvertedAmount }, ct: ct);
    }

    public async Task<IEnumerable<Expense>> GetByPaymentMethodIdAsync(
        Guid paymentMethodId, CancellationToken ct = default)
        => await _expenseRepo.GetByPaymentMethodIdAsync(paymentMethodId, ct);
}
