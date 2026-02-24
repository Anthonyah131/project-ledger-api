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
    private readonly IPlanAuthorizationService _planAuth;

    public ExpenseService(
        IExpenseRepository expenseRepo,
        IProjectRepository projectRepo,
        IPlanAuthorizationService planAuth)
    {
        _expenseRepo = expenseRepo;
        _projectRepo = projectRepo;
        _planAuth = planAuth;
    }

    public async Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var expense = await _expenseRepo.GetByIdAsync(id, ct);
        return expense is { ExpIsDeleted: false } ? expense : null;
    }

    public async Task<IEnumerable<Expense>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await _expenseRepo.GetByProjectIdAsync(projectId, ct);

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

        // Calcular monto convertido
        expense.ExpConvertedAmount = expense.ExpOriginalAmount * expense.ExpExchangeRate;

        // Calcular monto alternativo si aplica
        if (expense.ExpAltCurrency is not null && expense.ExpAltExchangeRate.HasValue)
            expense.ExpAltAmount = expense.ExpOriginalAmount * expense.ExpAltExchangeRate.Value;

        expense.ExpCreatedAt = DateTime.UtcNow;
        expense.ExpUpdatedAt = DateTime.UtcNow;

        await _expenseRepo.AddAsync(expense, ct);
        await _expenseRepo.SaveChangesAsync(ct);

        return expense;
    }

    public async Task UpdateAsync(Expense expense, CancellationToken ct = default)
    {
        // Recalcular montos
        expense.ExpConvertedAmount = expense.ExpOriginalAmount * expense.ExpExchangeRate;
        expense.ExpAltAmount = expense.ExpAltCurrency is not null && expense.ExpAltExchangeRate.HasValue
            ? expense.ExpOriginalAmount * expense.ExpAltExchangeRate.Value
            : null;

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
    }
}
