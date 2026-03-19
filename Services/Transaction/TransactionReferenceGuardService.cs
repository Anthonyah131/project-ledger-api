using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;

namespace ProjectLedger.API.Services;

/// <summary>
/// Protege referencias de catálogo y relaciones del proyecto cuando todavía
/// existen gastos o ingresos activos que dependen de ellas.
/// </summary>
public class TransactionReferenceGuardService : ITransactionReferenceGuardService
{
    private readonly AppDbContext _context;

    public TransactionReferenceGuardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task EnsureCategoryCanBeDeletedAsync(Guid categoryId, CancellationToken ct = default)
    {
        var hasActiveReferences = await _context.Expenses.AnyAsync(
                                      expense => expense.ExpCategoryId == categoryId && !expense.ExpIsDeleted, ct)
                                  || await _context.Incomes.AnyAsync(
                                      income => income.IncCategoryId == categoryId && !income.IncIsDeleted, ct);

        if (hasActiveReferences)
        {
            throw new InvalidOperationException(
                "No se puede eliminar la categoría porque tiene gastos o ingresos activos relacionados.");
        }
    }

    public async Task EnsurePaymentMethodCanBeDeletedAsync(Guid paymentMethodId, CancellationToken ct = default)
    {
        var hasActiveReferences = await _context.Expenses.AnyAsync(
                                      expense => expense.ExpPaymentMethodId == paymentMethodId && !expense.ExpIsDeleted, ct)
                                  || await _context.Incomes.AnyAsync(
                                      income => income.IncPaymentMethodId == paymentMethodId && !income.IncIsDeleted, ct);

        if (hasActiveReferences)
        {
            throw new InvalidOperationException(
                "No se puede eliminar el metodo de pago porque tiene gastos o ingresos activos relacionados.");
        }
    }

    public async Task EnsureAlternativeCurrencyCanBeRemovedAsync(
        Guid projectId,
        string currencyCode,
        CancellationToken ct = default)
    {
        var normalizedCurrencyCode = currencyCode.ToUpperInvariant();

        var hasActiveReferences = await _context.TransactionCurrencyExchanges.AnyAsync(
            exchange => exchange.TceCurrencyCode == normalizedCurrencyCode
                && ((exchange.Expense != null
                        && exchange.Expense.ExpProjectId == projectId
                        && !exchange.Expense.ExpIsDeleted)
                    || (exchange.Income != null
                        && exchange.Income.IncProjectId == projectId
                        && !exchange.Income.IncIsDeleted)),
            ct);

        if (hasActiveReferences)
        {
            throw new InvalidOperationException(
                "No se puede eliminar la moneda alternativa porque hay gastos o ingresos activos que la usan en este proyecto.");
        }
    }

    public async Task EnsureProjectPaymentMethodCanBeUnlinkedAsync(
        Guid projectId,
        Guid paymentMethodId,
        CancellationToken ct = default)
    {
        var hasActiveReferences = await _context.Expenses.AnyAsync(
                                      expense => expense.ExpProjectId == projectId
                                          && expense.ExpPaymentMethodId == paymentMethodId
                                          && !expense.ExpIsDeleted,
                                      ct)
                                  || await _context.Incomes.AnyAsync(
                                      income => income.IncProjectId == projectId
                                          && income.IncPaymentMethodId == paymentMethodId
                                          && !income.IncIsDeleted,
                                      ct);

        if (hasActiveReferences)
        {
            throw new InvalidOperationException(
                "No se puede desvincular el metodo de pago del proyecto porque tiene gastos o ingresos activos relacionados en este proyecto.");
        }
    }
}